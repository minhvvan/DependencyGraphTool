
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public class DependencyAnalyzer
{
    public void AnalyzeAssetRecursively(Dictionary<string, AssetNode> assetNodes, string assetPath, HashSet<string> processedAssets, int depth, int maxDepth = 5)
    {
        // 재귀 깊이 제한 또는 이미 처리된 에셋이면 중단
        if (depth >= maxDepth || processedAssets.Contains(assetPath))
            return;
        
        // 현재 에셋의 노드 가져오기
        AssetNode currentNode = assetNodes[assetPath];
        
        // 에셋 처리 표시
        processedAssets.Add(assetPath);

        // 에셋 타입에 따른 분석
        string ext = System.IO.Path.GetExtension(assetPath).ToLower();
        List<string> dependencies = new List<string>();

        if (ext == ".cs")
        {
            // 스크립트 분석
            dependencies = AnalyzeScript(assetPath);
        }
        else
        {
            // 일반 에셋 분석
            dependencies = AnalyzeAsset(assetPath);
        }

        // 발견된 모든 종속성에 대해
        foreach (string dependency in dependencies)
        {
            // 노드의 종속성 목록에 추가
            if (!currentNode.Dependencies.Contains(dependency))
            {
                currentNode.Dependencies.Add(dependency);
            }
            
            // 타겟 노드가 없으면 생성
            if (!assetNodes.ContainsKey(dependency))
            {
                AssetNode dependencyNode = new AssetNode(dependency);
                dependencyNode.Depth = depth + 1;
                dependencyNode.Parent = currentNode;
                assetNodes[dependency] = dependencyNode;
                
                // 부모-자식 관계 설정
                currentNode.Children.Add(dependencyNode);
            }
            else
            {
                AssetNode existingNode = assetNodes[dependency];
                
                // 더 짧은 경로가 발견되면 깊이와 부모 업데이트
                if (depth + 1 < existingNode.Depth)
                {
                    // 기존 부모에서 제거
                    if (existingNode.Parent != null)
                    {
                        existingNode.Parent.Children.Remove(existingNode);
                    }
                    
                    // 새 부모로 연결
                    existingNode.Parent = currentNode;
                    existingNode.Depth = depth + 1;
                    
                    // 부모의 자식 목록에 추가
                    if (!currentNode.Children.Contains(existingNode))
                    {
                        currentNode.Children.Add(existingNode);
                    }
                }
            }
            
            // 재귀적으로 종속성 분석
            AnalyzeAssetRecursively(assetNodes, dependency, processedAssets, depth + 1, maxDepth);
        }
    }
    
        private List<string> AnalyzeAsset(string assetPath)
    {
        List<string> result = new List<string>();
    
        // 스크립트, 프리팹, 에셋, 머티리얼만 분석
        string[] allowedExtensions = { ".cs", ".prefab", ".asset"};
    
        string[] dependencies = AssetDatabase.GetDependencies(assetPath, false);
    
        foreach (string dep in dependencies)
        {
            string ext = System.IO.Path.GetExtension(dep).ToLower();
        
            // 허용된 확장자만 추가
            if (allowedExtensions.Contains(ext) && 
                dep != assetPath && 
                !dep.StartsWith("Packages/") && 
                !dep.StartsWith("Library/"))
            {
                result.Add(dep);
            }
        }
    
        return result;
    }

    private List<string> AnalyzeScript(string scriptPath)
    {
        List<string> result = new List<string>();
        
        try
        {
            // 스크립트에서 타입 가져오기
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
            if (script == null) return result;
            
            Type scriptType = script.GetClass();
            if (scriptType == null) 
            {
                return result;
            }
            
            // 프로젝트 내 모든 C# 스크립트 가져오기
            string[] allScriptGuids = AssetDatabase.FindAssets("t:MonoScript");
            
            foreach (string guid in allScriptGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path == scriptPath || !path.EndsWith(".cs")) continue;
                
                MonoScript otherScript = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (otherScript == null) continue;
                
                Type otherType = otherScript.GetClass();
                if (otherType == null) continue;
                
                // 스크립트 타입 간의 참조 관계 확인
                if (HasTypeReference(scriptType, otherType))
                {
                    result.Add(path);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"어셈블리 분석 오류: {e.Message}");
        }
        
        return result;
    }

        private bool HasTypeReference(Type sourceType, Type targetType)
    {
        try
        {
            string targetTypeName = targetType.Name;
            
            // 1. 상속 관계 확인
            if (sourceType.BaseType != null &&
                (sourceType.BaseType == targetType || sourceType.BaseType.Name == targetTypeName))
            {
                return true;
            }
                
            // 2. 인터페이스 구현 확인
            foreach (Type interfaceType in sourceType.GetInterfaces())
            {
                if (interfaceType == targetType || interfaceType.Name == targetTypeName)
                {
                    return true;
                }
            }
            
            // 3. 필드 타입 확인
            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | 
                                BindingFlags.Instance | BindingFlags.Static;
                                
            foreach (FieldInfo field in sourceType.GetFields(flags))
            {
                Type fieldType = field.FieldType;
                
                // 배열인 경우 요소 타입 확인
                if (fieldType.IsArray && fieldType.GetElementType() == targetType)
                {
                    return true;
                }
                    
                // 제네릭 타입인 경우 인자 타입 확인
                if (fieldType.IsGenericType)
                {
                    foreach (Type argType in fieldType.GetGenericArguments())
                    {
                        if (argType == targetType || argType.Name == targetTypeName)
                        {
                            return true;
                        }
                    }
                }
                
                // 직접 타입 비교
                if (fieldType == targetType || fieldType.Name == targetTypeName)
                {
                    return true;
                }
            }
            
            // 4. 메서드 파라미터 및 반환 타입 확인
            foreach (MethodInfo method in sourceType.GetMethods(flags))
            {
                // 반환 타입 확인
                if (method.ReturnType == targetType || method.ReturnType.Name == targetTypeName)
                {
                    return true;
                }
                    
                // 파라미터 타입 확인
                foreach (ParameterInfo param in method.GetParameters())
                {
                    Type paramType = param.ParameterType;
                    
                    if (paramType == targetType || paramType.Name == targetTypeName)
                    {
                        return true;
                    }
                        
                    // 제네릭 파라미터 확인
                    if (paramType.IsGenericType)
                    {
                        foreach (Type argType in paramType.GetGenericArguments())
                        {
                            if (argType == targetType || argType.Name == targetTypeName)
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            
            return false;
        }
        catch
        {
            // 오류 발생 시 안전하게 처리
            return false;
        }
    }

}
        