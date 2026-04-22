//using System;
//using UnityEngine;
//using static UnityEngine.Rendering.DebugUI;

//public class ObjectHp : MonoBehaviour
//{
//    Panel panel;
//    LazerPatten lazerPatten;
//    public float currentHp = 1f;
//    public bool objisDead = false;

//    public bool isPanel = false;
//    public bool isLazer = false;

//    public bool[] isLazerDead = new bool[4];
//    public bool[] isPanelDead = new bool[7];

//    private void Start()
//    {
//        panel=Panel.Instance;
//        lazerPatten=LazerPatten.Instance;
//    }
//    private void OnEnable()
//    {
//        currentHp = 1f;
//        objisDead = false;
//    }

//    public void OnDamage(float damage)
//    {
//        if (objisDead) return;

//        currentHp -= damage;
//        if (currentHp <= 0)
//        {
//            objisDead = true;
//            OnPanelDestroyed(gameObject); 
//            gameObject.SetActive(false);  
//        }
//    }
//    public void OnPanelDestroyed(GameObject destroyedPanel)
//    {
//        int index;
//        if (isLazer)
//        {
//            index = System.Array.IndexOf(lazerPatten.lazerObject, destroyedPanel);
//            lazerPatten.OnPanelDestroyed(gameObject);
//            Debug.Log($"파괴된 레이저 인덱스: {index}");
//        }     
//        if (isPanel)
//        {
//            index = System.Array.IndexOf(panel.panel, destroyedPanel);
//            panel.OnPanelDestroyed(gameObject);
//            Debug.Log($"파괴된 판넬 인덱스: {index}");
//        }
//    }
//}
