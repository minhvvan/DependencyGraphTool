using System;
using System.Collections.Generic;

public class CycleDetector
{
    public void DetectCycle(Dictionary<string, AssetNode> assetNodes, HashSet<Tuple<string, string>> cyclicEdges)
    {
        HashSet<string> visited = new HashSet<string>();
        HashSet<string> inStack = new HashSet<string>();
            
        // 모든 노드에 대해 순환 참조 검사
        foreach (var node in assetNodes.Keys)
        {
            if (!visited.Contains(node))
            {
                DFSForCycleDetection(node, visited, inStack, assetNodes, cyclicEdges);
            }
        }
    }
    
    private void DFSForCycleDetection(string node, HashSet<string> visited, HashSet<string> inStack, Dictionary<string, AssetNode> assetNodes, HashSet<Tuple<string, string>> cyclicEdges)
    {
        visited.Add(node);
        inStack.Add(node);
        
        foreach (var dep in assetNodes[node].Dependencies)
        {
            if (!visited.Contains(dep))
            {
                DFSForCycleDetection(dep, visited, inStack, assetNodes, cyclicEdges);
            }
            else if (inStack.Contains(dep))
            {
                // 사이클 발견
                cyclicEdges.Add(new Tuple<string, string>(node, dep));
                
                // 사이클 관련 노드 표시
                if (assetNodes.ContainsKey(node))
                    assetNodes[node].InCycle = true;
                if (assetNodes.ContainsKey(dep))
                    assetNodes[dep].InCycle = true;
            }
        }
        
        inStack.Remove(node);
    }
}
