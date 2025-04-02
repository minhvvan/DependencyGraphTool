using System.Collections.Generic;
using System.Linq;
using Unity.Tutorials.Core.Editor;
using UnityEngine;

public class NodePositionCalculator
{
    public float nodeWidth = 180f;
    public float nodeHeight = 40f;
    
    public void ArrangeNodesWithHierarchy(Dictionary<string, Rect> nodePositions, Vector2 canvasCenter, string selectedAsset, Dictionary<string, AssetNode> assetNodes)
    {
        nodePositions.Clear();

        // 가상 캔버스 중심점을 기준으로 좌표 계산
        float centerX = canvasCenter.x;
        float centerY = canvasCenter.y;

        // 1. 루트 노드 배치
        if (!selectedAsset.IsNullOrEmpty() && assetNodes.ContainsKey(selectedAsset))
        {
            nodePositions[selectedAsset] = new Rect(
                centerX - nodeWidth / 2,
                centerY - nodeHeight / 2,
                nodeWidth,
                nodeHeight
            );
        }

        // 충돌 감지 및 방지를 위한 HashSet
        HashSet<Rect> occupiedAreas = new HashSet<Rect>();

        // 이미 배치된 노드의 영역 추가
        foreach (var rect in nodePositions.Values)
        {
            occupiedAreas.Add(rect);
        }

        // 2. 깊이 1인 노드들을 360도로 균등 분포 (주요 방향성 설정)
        var depth1Nodes = assetNodes.Values
            .Where(node => node.Depth == 1)
            .ToList();

        int totalDirections = Mathf.Max(8, depth1Nodes.Count); // 최소 8방향 보장

        // 각 노드가 담당할 방향 영역 할당
        Dictionary<string, float> nodeBaseAngles = new Dictionary<string, float>();

        for (int i = 0; i < depth1Nodes.Count; i++)
        {
            AssetNode node = depth1Nodes[i];

            // 360도를 균등하게 분할
            float angle = (2f * Mathf.PI * i) / totalDirections;
            nodeBaseAngles[node.Path] = angle;

            // 초기 위치 계산
            float radius = 350f; // 1단계 깊이 반지름

            float x = centerX + Mathf.Cos(angle) * radius - nodeWidth / 2;
            float y = centerY + Mathf.Sin(angle) * radius - nodeHeight / 2;

            // 노드 배치
            Rect newRect = new Rect(
                Mathf.Max(0, x),
                Mathf.Max(0, y),
                nodeWidth,
                nodeHeight
            );

            // 충돌 검사 및 회피
            while (IsOverlapping(newRect, occupiedAreas))
            {
                radius += 50f;
                x = centerX + Mathf.Cos(angle) * radius - nodeWidth / 2;
                y = centerY + Mathf.Sin(angle) * radius - nodeHeight / 2;

                newRect = new Rect(
                    Mathf.Max(0, x),
                    Mathf.Max(0, y),
                    nodeWidth,
                    nodeHeight
                );
            }

            nodePositions[node.Path] = newRect;
            occupiedAreas.Add(newRect);
        }

        // 3. 나머지 깊이의 노드들을 자신의 부모나 참조 노드의 방향으로 분포
        int maxDepth = assetNodes.Values.Any() ? assetNodes.Values.Max(n => n.Depth) : 0;

        for (int depth = 2; depth <= maxDepth; depth++)
        {
            var nodesInDepth = assetNodes.Values
                .Where(node => node.Depth == depth)
                .ToList();

            // 부모별로 자식 노드들을 그룹화
            var nodesByParent = nodesInDepth.GroupBy(n => n.Parent.Path).ToList();

            // 각 부모 노드에 대해 자식들을 분산 배치
            foreach (var parentGroup in nodesByParent)
            {
                string parentPath = parentGroup.Key;
                var childNodes = parentGroup.ToList();
                int childCount = childNodes.Count;

                if (!nodePositions.ContainsKey(parentPath)) continue;

                AssetNode parentNode = assetNodes[parentPath];
                Rect parentRect = nodePositions[parentPath];
                Vector2 parentCenter = new Vector2(
                    parentRect.x + parentRect.width / 2,
                    parentRect.y + parentRect.height / 2
                );

                // 성장 방향 결정 (부모-조부모 방향 사용)
                float growthAngle = 0;

                if (parentNode.Parent != null && nodePositions.ContainsKey(parentNode.Parent.Path))
                {
                    Rect grandparentRect = nodePositions[parentNode.Parent.Path];
                    Vector2 grandparentCenter = new Vector2(
                        grandparentRect.x + grandparentRect.width / 2,
                        grandparentRect.y + grandparentRect.height / 2
                    );

                    Vector2 growthDir = new Vector2(
                        parentCenter.x - grandparentCenter.x,
                        parentCenter.y - grandparentCenter.y
                    );

                    if (growthDir.magnitude > 1.0f)
                    {
                        growthAngle = Mathf.Atan2(growthDir.y, growthDir.x);
                    }
                }

                // 자식 노드 개수에 따른 배치 전략
                float baseRadius = 60f + (depth * 120f);
                float angleSpread;

                // 자식 수에 따라 분산 각도 조정
                if (childCount <= 3)
                {
                    angleSpread = Mathf.PI / 6; // 30도 (±15도)
                }
                else if (childCount <= 6)
                {
                    angleSpread = Mathf.PI / 4; // 45도 (±22.5도)
                }
                else
                {
                    angleSpread = Mathf.PI / 3; // 60도 (±30도)
                }

                // 각 자식 노드 배치
                for (int i = 0; i < childCount; i++)
                {
                    AssetNode childNode = childNodes[i];

                    // 이미 배치된 노드는 건너뛰기
                    if (nodePositions.ContainsKey(childNode.Path)) continue;

                    // 자식 위치 계산 (부모로부터 일정 거리, 분산된 각도)
                    float childAngle = growthAngle;

                    if (childCount > 1)
                    {
                        // -1.0 ~ +1.0 범위로 정규화된 오프셋
                        float normalizedOffset = (2.0f * i / (childCount - 1)) - 1.0f;
                        childAngle += angleSpread * normalizedOffset;
                    }

                    // 충돌 회피를 위한 위치 시도
                    bool positionFound = false;
                    float radius = baseRadius;
                    int attempts = 0;

                    while (!positionFound && attempts < 12)
                    {
                        float x = parentCenter.x + Mathf.Cos(childAngle) * radius - nodeWidth / 2;
                        float y = parentCenter.y + Mathf.Sin(childAngle) * radius - nodeHeight / 2;

                        Rect newRect = new Rect(
                            Mathf.Max(0, x),
                            Mathf.Max(0, y),
                            nodeWidth,
                            nodeHeight
                        );

                        if (!IsOverlapping(newRect, occupiedAreas))
                        {
                            nodePositions[childNode.Path] = newRect;
                            occupiedAreas.Add(newRect);
                            positionFound = true;
                        }
                        else
                        {
                            attempts++;

                            // 충돌 회피 전략 수정 - 각도와 거리 모두 조정
                            if (attempts % 2 == 0)
                            {
                                // 거리 증가
                                radius += 50f;
                            }
                            else
                            {
                                // 각도 미세 조정 (방향 번갈아가며)
                                float angleAdjust = (Mathf.PI / 12) * (attempts % 4 == 1 ? 1 : -1);
                                childAngle += angleAdjust;
                            }
                        }
                    }

                    // 위치를 찾지 못했다면 강제 배치
                    if (!positionFound)
                    {
                        float x = parentCenter.x + Mathf.Cos(childAngle) * (baseRadius + 300f) - nodeWidth / 2;
                        float y = parentCenter.y + Mathf.Sin(childAngle) * (baseRadius + 300f) - nodeHeight / 2;

                        Rect newRect = new Rect(
                            Mathf.Max(0, x),
                            Mathf.Max(0, y),
                            nodeWidth,
                            nodeHeight
                        );

                        nodePositions[childNode.Path] = newRect;
                        occupiedAreas.Add(newRect);
                    }
                }
            }
        }
    }
    
    private bool IsOverlapping(Rect rect, HashSet<Rect> existingRects)
    {
        foreach (var existingRect in existingRects)
        {
            // 여백을 추가하여 충분한 간격 보장
            Rect expandedExisting = new Rect(
                existingRect.x - 10,
                existingRect.y - 10,
                existingRect.width + 20,
                existingRect.height + 20
            );
        
            if (rect.Overlaps(expandedExisting))
            {
                return true;
            }
        }
    
        return false;
    }
}
