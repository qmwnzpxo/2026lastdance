using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LazerPatten : MonoBehaviour
{
    [Header("Lazer Node Object")]
    public GameObject[] lazerObject = new GameObject[4];

    [Header("Line Objects")]
    public GameObject[] lazers = new GameObject[4];

    [Header("Lazer Setting")]
    public float firstScle = 0.1f;
    public float lastScale = 1f;
    public float RotateSpeed = 300f;

    [Header("Line Renderer Setting")]
    public float lineWidth = 0.05f;
    public Color lineColor = Color.red;

    [Header("판정 오브젝트 두께")]
    public float lazerThickness = 0.1f;

    [Header("테스트")]
    public bool destroyTest0 = false;
    public bool destroyTest1 = false;
    public bool destroyTest2 = false;
    public bool destroyTest3 = false;

    private LineRenderer[] lineRenderers;
    private bool[] prevDestroyTest = new bool[4];


    private void Awake()
    {
        foreach (GameObject lazer in lazerObject)
            if (lazer != null) lazer.SetActive(false);

        foreach (GameObject l in lazers)
            if (l != null) l.SetActive(false);

        lineRenderers = new LineRenderer[lazers.Length];
        for (int i = 0; i < lazers.Length; i++)
        {
            if (lazers[i] == null) continue;

            LineRenderer lr = lazers[i].GetComponent<LineRenderer>();
            if (lr == null) lr = lazers[i].AddComponent<LineRenderer>();

            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = lineColor;
            lr.endColor = lineColor;

            lineRenderers[i] = lr;
        }

        StartCoroutine(ActivateLazers());
    }

    private IEnumerator ActivateLazers()
    {
        yield return new WaitForSecondsRealtime(0.5f);

        for (int i = 0; i < lazerObject.Length; i++)
        {
            yield return new WaitForSecondsRealtime(0.5f);
            if (lazerObject[i] != null && !lazerObject[i].activeSelf)
                lazerObject[i].SetActive(true);
        }

        for (int i = 0; i < lazers.Length; i++)
        {
            yield return new WaitForSecondsRealtime(0.5f);
            if (lazers[i] != null && !lazers[i].activeSelf)
                lazers[i].SetActive(true);
        }
    }

    private void FixedUpdate()
    {
        ApplyDestroyTests();

        foreach (GameObject lazer in lazerObject)
        {
            if (lazer == null || !lazer.activeSelf) continue;

            lazer.transform.Rotate(Vector3.up * RotateSpeed * Time.fixedDeltaTime, Space.World);

            if (lazer.transform.localScale.x < lastScale)
            {
                float newScale = Mathf.Min(lazer.transform.localScale.x + firstScle, lastScale);
                lazer.transform.localScale = new Vector3(newScale, 1f, newScale);
            }
        }

        UpdateLines();
    }

    private void ApplyDestroyTests()
    {
        bool[] current = { destroyTest0, destroyTest1, destroyTest2, destroyTest3 };

        for (int i = 0; i < lazerObject.Length; i++)
        {
            if (lazerObject[i] == null) continue;

            if (current[i] && !prevDestroyTest[i])
                lazerObject[i].SetActive(false);

            if (!current[i] && prevDestroyTest[i])
                lazerObject[i].SetActive(true);

            prevDestroyTest[i] = current[i];
        }
    }

    private void UpdateLines()
    {

        List<GameObject> alive = new List<GameObject>();
        foreach (GameObject lazer in lazerObject)
            if (lazer != null && lazer.activeSelf)
                alive.Add(lazer);

        int aliveCount = alive.Count;

        if (aliveCount < 2)
        {
            foreach (GameObject l in lazers)
                if (l != null) l.SetActive(false);
            return;
        }

        Vector3 worldCenter = Vector3.zero;
        foreach (GameObject node in alive)
            worldCenter += node.transform.position;
        worldCenter /= aliveCount;

        Camera cam = Camera.main;

        alive.Sort((a, b) =>
        {
            float angleA, angleB;
            if (cam != null)
            {
                Vector2 sc = cam.WorldToScreenPoint(worldCenter);
                Vector2 sA = cam.WorldToScreenPoint(a.transform.position);
                Vector2 sB = cam.WorldToScreenPoint(b.transform.position);
                angleA = Mathf.Atan2((sA - sc).y, (sA - sc).x);
                angleB = Mathf.Atan2((sB - sc).y, (sB - sc).x);
            }
            else
            {
                Vector3 dA = a.transform.position - worldCenter;
                Vector3 dB = b.transform.position - worldCenter;
                angleA = Mathf.Atan2(dA.z, dA.x);
                angleB = Mathf.Atan2(dB.z, dB.x);
            }
            return angleA.CompareTo(angleB);
        });

  
        for (int i = 0; i < lazers.Length; i++)
        {
            if (lazers[i] == null) continue;

            if (i < aliveCount)
            {
                Vector3 posA = alive[i].transform.position;
                Vector3 posB = alive[(i + 1) % aliveCount].transform.position;

                float dist = Vector3.Distance(posA, posB);
                Vector3 midPos = (posA + posB) / 2f;
                Vector3 dir = (posB - posA).normalized;

     
                lazers[i].transform.position = midPos;

                if (dir != Vector3.zero)
                    lazers[i].transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, 90f, 0f);

                lazers[i].transform.localScale = new Vector3(dist, lazerThickness, lazerThickness);

                lazers[i].SetActive(true);

                lineRenderers[i].SetPosition(0, posA);
                lineRenderers[i].SetPosition(1, posB);
            }
            else
            {
                lazers[i].SetActive(false);
            }
        }
    }
}