using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[CreateAssetMenu(fileName = "StageDatabase", menuName = "Puzzle/Stage Database")]
public class StageDatabase : ScriptableObject
{
    [SerializeField]
    private List<StageDataEntry> stages = new List<StageDataEntry>();
    
    public List<StageDataEntry> Stages => stages;
    
    // 스테이지 추가/제거
    public void AddStage(StageData data)
    {
        stages.Add(new StageDataEntry(data));
    }
    
    public void RemoveStage(int stageId)
    {
        stages.RemoveAll(s => s.data.stageId == stageId);
    }
    
    public void ClearAll()
    {
        stages.Clear();
    }
    
    // 정렬 메서드
    public List<StageDataEntry> GetSortedStages(SortCriteria criteria, bool ascending = true)
    {
        var sorted = stages.AsEnumerable();
        
        switch (criteria)
        {
            case SortCriteria.ID:
                sorted = ascending 
                    ? sorted.OrderBy(s => s.data.stageId) 
                    : sorted.OrderByDescending(s => s.data.stageId);
                break;
            case SortCriteria.Difficulty:
                sorted = ascending 
                    ? sorted.OrderBy(s => s.data.difficulty) 
                    : sorted.OrderByDescending(s => s.data.difficulty);
                break;
            case SortCriteria.ColorCount:
                sorted = ascending 
                    ? sorted.OrderBy(s => s.data.colorCount) 
                    : sorted.OrderByDescending(s => s.data.colorCount);
                break;
            case SortCriteria.SolutionMoves:
                sorted = ascending 
                    ? sorted.OrderBy(s => s.data.solutionMoves) 
                    : sorted.OrderByDescending(s => s.data.solutionMoves);
                break;
        }
        
        return sorted.ToList();
    }
    
    // 필터 메서드
    public List<StageDataEntry> FilterStages(StageFilter filter)
    {
        var filtered = stages.AsEnumerable();
        
        // 난이도 필터
        if (filter.difficulties != null && filter.difficulties.Count > 0)
            filtered = filtered.Where(s => filter.difficulties.Contains(s.data.difficulty));
        
        // 색상 개수 필터
        if (filter.minColors > 0)
            filtered = filtered.Where(s => s.data.colorCount >= filter.minColors);
        
        if (filter.maxColors > 0)
            filtered = filtered.Where(s => s.data.colorCount <= filter.maxColors);
        
        // 이동 횟수 필터
        if (filter.minMoves > 0)
            filtered = filtered.Where(s => s.data.solutionMoves >= filter.minMoves);
        
        if (filter.maxMoves > 0)
            filtered = filtered.Where(s => s.data.solutionMoves <= filter.maxMoves);
        
        // 검색 쿼리
        if (!string.IsNullOrEmpty(filter.searchQuery))
        {
            string query = filter.searchQuery.ToLower();
            filtered = filtered.Where(s => 
                s.data.stageId.ToString().Contains(query) ||
                (s.data.placementStrategy != null && s.data.placementStrategy.ToLower().Contains(query)));
        }
        
        return filtered.ToList();
    }
    
    // Resources에서 모든 스테이지 로드
    public void LoadAllStagesFromResources()
    {
        stages.Clear();
        TextAsset[] jsonFiles = Resources.LoadAll<TextAsset>("Stages");
        
        foreach (var jsonFile in jsonFiles)
        {
            try
            {
                StageData data = JsonUtility.FromJson<StageData>(jsonFile.text);
                if (data != null)
                {
                    AddStage(data);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Failed to load stage from {jsonFile.name}: {e.Message}");
            }
        }
        
        Debug.Log($"Loaded {stages.Count} stages from Resources");
    }
    
    // 스테이지 찾기
    public StageDataEntry FindStage(int stageId)
    {
        return stages.Find(s => s.data.stageId == stageId);
    }
}

[System.Serializable]
public class StageDataEntry
{
    public StageData data;
    public Texture2D thumbnail;  // 썸네일 캐시
    
    public StageDataEntry(StageData data)
    {
        this.data = data;
        this.thumbnail = null;
    }
}

public enum SortCriteria
{
    ID,
    Difficulty,
    ColorCount,
    SolutionMoves
}

[System.Serializable]
public class StageFilter
{
    public List<int> difficulties = new List<int>();
    public int minColors = 0;
    public int maxColors = 0;
    public int minMoves = 0;
    public int maxMoves = 0;
    public string searchQuery = "";
    
    public void Reset()
    {
        difficulties.Clear();
        minColors = 0;
        maxColors = 0;
        minMoves = 0;
        maxMoves = 0;
        searchQuery = "";
    }
}
