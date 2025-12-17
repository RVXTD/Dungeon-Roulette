using UnityEngine;

public class WeaponRandomizer : MonoBehaviour
{
    [Header("Existing Weapon Objects")]
    [SerializeField] private GameObject weapon1;
    [SerializeField] private GameObject weapon2;
    [SerializeField] private GameObject weapon3;
    [SerializeField] private GameObject weapon4;

    void Start()
    {
        DisableAllWeapons();
        Invoke("PickWeapon", 1);
    }

    void PickWeapon()
    {
        int roll = Random.Range(1, 5); // 1–4

        switch (roll)
        {
            case 1:
                weapon1.SetActive(true);
                Debug.Log("Rolled 1");
                break;
            case 2:
                weapon2.SetActive(true);
                Debug.Log("Rolled 2");
                break;
            case 3:
                weapon3.SetActive(true);
                Debug.Log("Rolled 3");
                break;
            case 4:
                weapon4.SetActive(true);
                Debug.Log("Rolled 4");
                break;
        }
    }

    void DisableAllWeapons()
    {
        if (weapon1) weapon1.SetActive(false);
        if (weapon2) weapon2.SetActive(false);
        if (weapon3) weapon3.SetActive(false);
        if (weapon4) weapon4.SetActive(false);
    }
}
