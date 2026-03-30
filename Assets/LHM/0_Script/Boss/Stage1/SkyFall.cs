using UnityEngine;

public class SkyFall : MonoBehaviour
{
    //위에 오브젝트 스폰
    //3초 있다가 떨어짐
    //큰 오브젝트는 바로 아래 y기준 x+-5에 화면상에서 경고표시

    public GameObject[] smallStone = new GameObject[5];
    public GameObject[] largeStone = new GameObject[5];

    public float FallSpeed = 1.0f;
    public float LargeFallSpeed = 1.0f;
   
    public GameObject objectPrefab;
    public float minX = -10f;
    public float maxX = 10f;
    public float minY = 0f;
    public float maxY = 10f;
    public float minZ = 1f;
    public float maxZ = 1f;
    public bool is3D = true;

    EnemyBase enemyBase; 
    public void SpawnObject()
    {
        float x = Random.Range(minX, maxX);
        float y = Random.Range(minY, maxY);
        float z = Random.Range(minZ, maxZ);

        Vector3 randomPosition;

        if (is3D)
        {
            randomPosition = new Vector3(x, y, z);
        }
        else
        {
            
            randomPosition = new Vector3(x, y, 0);
        }

        
        Instantiate(objectPrefab, randomPosition, Quaternion.identity);
    }

  
    void Start()
    {
        InvokeRepeating("SpawnObject", 2f, 2f); 
    }
    void PlayerDis()
    {
        if (enemyBase.playerTransform.position.y < transform.position.y)
        {
            objectPrefab.gameObject.SetActive(false);
        }
            
   
        
    }

}
