using UnityEngine;
using System.IO;

public class StageLoader : MonoBehaviour
{
    public StageData LoadStage(int stageId)
    {
        string fileName = $"stage_{stageId:000}";
        TextAsset jsonFile = null;

        // 먼저 루트 Stages 폴더에서 찾기
        string filePath = $"Stages/{fileName}";
        jsonFile = Resources.Load<TextAsset>(filePath);

        // 루트에서 못 찾으면 난이도 폴더에서 찾기
        if (jsonFile == null)
        {
            string[] difficultyFolders = { "Stages/1. Easy", "Stages/2. Medium", "Stages/3. Hard" };
            foreach (string folder in difficultyFolders)
            {
                filePath = $"{folder}/{fileName}";
                jsonFile = Resources.Load<TextAsset>(filePath);
                if (jsonFile != null)
                {
                    break; // 찾으면 루프 종료
                }
            }
        }

        if (jsonFile == null)
        {
            Debug.LogError($"스테이지 {stageId} 파일을 찾을 수 없습니다: Stages 폴더에서 찾을 수 없음");
            return null;
        }

        StageData data = JsonUtility.FromJson<StageData>(jsonFile.text);

        if (!ValidateStageData(data))
        {
            Debug.LogError($"스테이지 {stageId} 데이터가 유효하지 않습니다");
            return null;
        }

        return data;
    }

    private bool ValidateStageData(StageData data)
    {
        // 그리드 크기 검증
        if (data.gridSize.x <= 0 || data.gridSize.y <= 0) return false;

        // gridData 크기 검증 (1D 배열)
        int expectedLength = data.gridSize.x * data.gridSize.y;
        if (data.gridData == null || data.gridData.Length != expectedLength) return false;

        // 플레이어 시작 위치 검증
        if (data.playerStartPosition.x < 0 || data.playerStartPosition.x >= data.gridSize.x) return false;
        if (data.playerStartPosition.y < 0 || data.playerStartPosition.y >= data.gridSize.y) return false;

        return true;
    }
}
