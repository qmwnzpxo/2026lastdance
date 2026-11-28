using UnityEngine;

/// <summary>
/// 메인 메뉴 버튼 연결용. 버튼 OnClick 이벤트에 이 컴포넌트의 함수를 연결한다.
///   - Continue()  : 저장된 챕터 씬·구역에서 이어하기
///   - ClearSave() : 구역 저장 초기화 (새로 하기 등)
/// 버튼 활성/비활성은 HasSave로 판단.
/// </summary>
public class SectionSaveButton : MonoBehaviour
{
    public bool HasSave => SectionSave.HasSave;

    public void Continue() => SectionSave.Continue();

    public void ClearSave() => SectionSave.Clear();
}
