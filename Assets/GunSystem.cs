using UnityEngine;
using TMPro;
using UnityEngine.InputSystem; 

public class GunSystem : MonoBehaviour
{
    
    public int damage = 10;
    public float timeBetweenShooting = 0.2f;
    public float spread = 0.05f;
    public float range = 100f;
    public float reloadTime = 1.5f;
    public float timeBetweenShots = 0.1f;
    public int magazineSize = 8;
    public int bulletsPerTap = 1;
    public bool allowButtonHold = true;

    private int bulletsLeft;
    private int bulletsShot;
    private bool shooting;
    private bool readyToShoot = true;
    private bool reloading;

    public Camera fpsCam;
    public Transform attackPoint;
    public LayerMask whatIsEnemy;

    public GameObject muzzleFlash;
    public GameObject bulletHoleGraphic;
    public TextMeshProUGUI text;

    private void Awake()
    {
        bulletsLeft = magazineSize;
    }

    private void Update()
    {
        HandleInput();
        UpdateAmmoUI();
    }

    private void HandleInput()
    {
        
        bool isMouseDown = Mouse.current.leftButton.isPressed;
        bool justPressedReload = Keyboard.current.rKey.wasPressedThisFrame;

        shooting = allowButtonHold ? isMouseDown : Mouse.current.leftButton.wasPressedThisFrame;

        if (justPressedReload && bulletsLeft < magazineSize && !reloading)
            Reload();

        if (readyToShoot && shooting && !reloading && bulletsLeft > 0)
        {
            bulletsShot = bulletsPerTap;
            Shoot();
        }
    }

    private void Shoot()
    {
        if (fpsCam == null || attackPoint == null)
        {
            Debug.LogWarning("GunSystem: Missing camera or attack point reference.");
            return;
        }

        readyToShoot = false;

        // Random spread
        float x = Random.Range(-spread, spread);
        float y = Random.Range(-spread, spread);
        Vector3 direction = fpsCam.transform.forward + new Vector3(x, y, 0);

        if (Physics.Raycast(fpsCam.transform.position, direction, out RaycastHit hit, range, whatIsEnemy))
        {
            string hitName = hit.collider != null ? hit.collider.name : "Unknown";
            Debug.Log("Hit: " + hitName);

            if (hit.collider != null && hit.collider.CompareTag("Enemy"))
            {
                ShootingAi enemy = hit.collider.GetComponent<ShootingAi>();
                if (enemy != null)
                    enemy.TakeDamage(damage);
            }

            if (bulletHoleGraphic != null && hit.collider != null)
                Instantiate(bulletHoleGraphic, hit.point, Quaternion.LookRotation(hit.normal));
        }

        if (muzzleFlash != null)
            Instantiate(muzzleFlash, attackPoint.position, attackPoint.rotation);

        bulletsLeft--;
        bulletsShot--;

        Invoke(nameof(ResetShot), timeBetweenShooting);

        if (bulletsShot > 0 && bulletsLeft > 0)
            Invoke(nameof(Shoot), timeBetweenShots);
    }

    private void ResetShot()
    {
        readyToShoot = true;
    }

    private void Reload()
    {
        reloading = true;
        Debug.Log("Reloading...");
        Invoke(nameof(ReloadFinished), reloadTime);
    }

    private void ReloadFinished()
    {
        bulletsLeft = magazineSize;
        reloading = false;
        Debug.Log("Reloaded");
    }

    private void UpdateAmmoUI()
    {
        if (text != null)
            text.SetText($"{bulletsLeft} / {magazineSize}");
    }
}


public class ShootingAi : MonoBehaviour
{
    public int health = 50;

    public void TakeDamage(int damage)
    {
        if (!this) return;
        health -= damage;
        Debug.Log($"{gameObject.name} took {damage} damage. Health left: {health}");
        if (health <= 0)
        {
            Debug.Log($"{gameObject.name} destroyed!");
            Destroy(gameObject);
        }
    }
}
