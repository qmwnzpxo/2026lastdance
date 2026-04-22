//using Cysharp.Threading.Tasks;
//using UnityEngine;

//public class OutsiedLaser : MonoBehaviour
//{
//    [Header("cs")]
//    BossBase bossBase;

//    [Header("obj")]
//    // laserObject[0] = 왼쪽, [1] = 위쪽, [2] = 오른쪽
//    public GameObject[] laserObject;

//    [Header("transform")]
//    Transform playertransform;

//    [Header("line")]
//    public LineRenderer lineRenderer;
//    public float lineWidth = 4f;
//    public Color lineColor = Color.red;

//    [Header("map")]
//    public float mapMinX = -10f;
//    public float mapMaxX = 70f;
//    public float mapMaxY = 70f;
//    public float mapMinY = -10f;

//    // BUG 3 수정 - 중복 실행 방어 플래그
//    bool isRunning = false;


//    private void Start()
//    {
//        // 추가 - playertransform 초기화
//        playertransform = GameObject.FindWithTag("Player").transform;

//        if (playertransform == null)
//            Debug.LogError("[OutsiedLaser] Player 태그를 가진 오브젝트가 없습니다!");

//        lineRenderer = GetComponent<LineRenderer>();

//        if (lineRenderer == null)
//        {
//            lineRenderer = gameObject.AddComponent<LineRenderer>();
//            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
//            Debug.Log("[OutsiedLaser] LineRenderer가 없어서 자동 추가했습니다.");
//        }

//        // 추가 - laserObject null 체크
//        if (laserObject != null)
//            offgameobject();
//        else
//            Debug.LogError("[OutsiedLaser] laserObject 배열이 Inspector에서 할당되지 않았습니다!");
//    }
//    private void Update()
//    {
//        // T키 누르면 레이저 시퀀스 시작
//        if (Input.GetKeyDown(KeyCode.T))
//        {
//            Debug.Log("[Test] Phase2 Laser 시작");
//            StartPhase2Laser();
//        }
//    }
//    private void offgameobject()
//    {
//        for (int i = 0; i < laserObject.Length; i++)
//        {
//            if (laserObject[i] != null)
//                laserObject[i].SetActive(false);
//            else
//                Debug.LogWarning($"[OutsiedLaser] laserObject[{i}] 가 비어있습니다!");
//        }
//    }

//    private async UniTask ShowWarningLine(int direction, Vector3 fixedPlayerPos)
//    {
//        if (lineRenderer == null) return;

//        Vector3 startPos;
//        Vector3 endPos;

//        switch (direction)
//        {
//            case 0:
//                startPos = new Vector3(mapMinX - 60f, fixedPlayerPos.y, 0f);
//                endPos = new Vector3(mapMaxX + 60f, fixedPlayerPos.y, 0f);
//                lineWidth = 6f; // 수평선은 기본 두께
//                break;
//            case 1:
//                startPos = new Vector3(fixedPlayerPos.x, mapMaxY + 60f, 0f);
//                endPos = new Vector3(fixedPlayerPos.x, mapMinY - 60f, 0f);
//                lineWidth = 30f; // 수직선은 더 두껍게
//                break;
//            case 2:
//                startPos = new Vector3(mapMaxX + 60f, fixedPlayerPos.y, 0f);
//                endPos = new Vector3(mapMinX - 60f, fixedPlayerPos.y, 0f);
//                lineWidth= 6f; // 수평선은 기본 두께
//                break;
//            default:
//                return;
//        }

//        // BUG 수정 - 월드 좌표 명시
//        lineRenderer.useWorldSpace = true;

//        // BUG 수정 - Material 없으면 기본 Material 자동 할당
//        if (lineRenderer.material == null)
//            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));

//        lineRenderer.positionCount = 2;
//        lineRenderer.SetPosition(0, startPos);
//        lineRenderer.SetPosition(1, endPos);
//        lineRenderer.startWidth = lineWidth;
//        lineRenderer.endWidth = lineWidth;
//        lineRenderer.startColor = lineColor;
//        lineRenderer.endColor = lineColor;
//        lineRenderer.enabled = true;

//        // BUG 수정 - try/finally로 경고선 반드시 꺼짐 보장
//        try
//        {
//            await UniTask.Delay(3000);
//        }
//        finally
//        {
//            lineRenderer.enabled = false;
//        }
//    }

//    // BUG 1 수정 - 경고선 시점의 고정 좌표를 받아서 레이저 위치에 적용
//    private void Applylaser(int direction, Vector3 fixedPlayerPos)
//    {
//        if (laserObject == null || direction >= laserObject.Length) return;

//        switch (direction)
//        {
//            case 0:
//                laserObject[0].transform.position = new Vector3(
//                    laserObject[0].transform.position.x,
//                    fixedPlayerPos.y, 0f);
//                laserObject[0].SetActive(true);
//                break;
//            case 1:
//                laserObject[1].transform.position = new Vector3(
//                    fixedPlayerPos.x,
//                    laserObject[1].transform.position.y, 0f);
//                laserObject[1].SetActive(true);
//                break;
//            case 2:
//                laserObject[2].transform.position = new Vector3(
//                    laserObject[2].transform.position.x,
//                    fixedPlayerPos.y, 0f);
//                laserObject[2].SetActive(true);
//                break;
//        }
//    }

//    private async UniTask LaserSequence()
//    {
//        int[] fireOrder = { 0, 1, 2 };

//        for (int i = 0; i < fireOrder.Length; i++)
//        {
//            int direction = fireOrder[i];

//            // BUG 1 수정 - 경고선 표시 시점에 플레이어 좌표 고정
//            Vector3 fixedPlayerPos = playertransform.position;

//            await ShowWarningLine(direction, fixedPlayerPos);

//            // 고정된 좌표로 레이저 발사 → 경고선과 위치 일치 보장
//            Applylaser(direction, fixedPlayerPos);

//            await UniTask.Delay(2000);

//            offgameobject();
//        }

//        // BUG 3 수정 - 시퀀스 종료 후 플래그 해제
//        isRunning = false;
//    }

//    public void StartPhase2Laser()
//    {
//        if (laserObject == null || laserObject.Length < 3)
//        {
//            Debug.LogError("[OutsiedLaser] laserObject 배열이 3개 설정되지 않았습니다!");
//            return;
//        }

//        // BUG 3 수정 - 중복 실행 방어
//        if (isRunning)
//        {
//            Debug.LogWarning("[OutsiedLaser] 이미 레이저 시퀀스가 실행 중입니다!");
//            return;
//        }

//        isRunning = true;
//        LaserSequence().Forget();
//    }
//}