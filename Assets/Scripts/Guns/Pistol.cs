using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

public class Pistol : MonoBehaviour
{
    
    public int damage = 20;
    public float timeBetweenShooting = 0.25f;
    public float spread = 0.02f;
    public float range = 100f;
    public float reloadTime = 1.2f;
    public float timeBetweenShots = 0.1f;
    public int magazineSize = 12;

    
    public bool allowButtonHold = false;
    public int bulletsPerTap = 1;

    
    public Camera fpsCam;
    public Transform attackPoint;
    public LayerMask whatIsEnemy;
    public GameObject muzzleFlash;
    public GameObject bulletHoleGraphic;
    public TextMeshProUGUI ammoText;

    private int bulletsLeft;
    private int bulletsShot;
    private bool shooting;
    private bool readyToShoot = true;
    private bool reloading;

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
            Debug.LogWarning("Pistol: Missing references!");
            return;
        }

        readyToShoot = false;

        // Spread
        float x = Random.Range(-spread, spread);
        float y = Random.Range(-spread, spread);
        Vector3 direction = fpsCam.transform.forward + new Vector3(x, y, 0);

        // Raycast
        if (Physics.Raycast(fpsCam.transform.position, direction, out RaycastHit hit, range, whatIsEnemy))
        {
            Debug.Log("Pistol Hit: " + hit.collider.name);

            // DAMAGE ENEMY (SUPPORTS EnemyHealth + IDamageable)
            if (hit.collider.CompareTag("Enemy"))
            {
                IDamageable dmg = hit.collider.GetComponent<IDamageable>();
                if (dmg != null)
                    dmg.TakeDamage(damage);
            }

            // Bullet hole
            if (bulletHoleGraphic != null)
                Instantiate(bulletHoleGraphic, hit.point, Quaternion.LookRotation(hit.normal));
        }

        // Muzzle flash
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
        Debug.Log("Pistol Reloading...");
        Invoke(nameof(ReloadFinished), reloadTime);
    }

    private void ReloadFinished()
    {
        bulletsLeft = magazineSize;
        reloading = false;
        Debug.Log("Pistol Reloaded");
    }

    private void UpdateAmmoUI()
    {
        if (ammoText != null)
            ammoText.SetText($"{bulletsLeft} / {magazineSize}");
    }
}
