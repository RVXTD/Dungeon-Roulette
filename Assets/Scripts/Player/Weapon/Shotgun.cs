using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

public class Shotgun : MonoBehaviour
{
    public int damage = 10;
    public int pellets = 8;
    public float timeBetweenShooting = 0.9f;
    public float spread = 0.12f;
    public float range = 60f;
    public float reloadTime = 2.2f;
    public int magazineSize = 8;

    public bool allowButtonHold = false;
    public int bulletsPerTap = 1;

    public Camera fpsCam;
    public Transform attackPoint;
    public LayerMask whatIsEnemy;
    public GameObject muzzleFlash;
    public GameObject bulletHoleGraphic;
    public TextMeshProUGUI ammoText;

    private int bulletsLeft;
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
            Shoot();
    }

    private void Shoot()
    {
        if (fpsCam == null || attackPoint == null)
            return;

        readyToShoot = false;

        for (int i = 0; i < pellets; i++)
        {
            float x = Random.Range(-spread, spread);
            float y = Random.Range(-spread, spread);
            Vector3 direction = fpsCam.transform.forward + new Vector3(x, y, 0);

            if (Physics.Raycast(fpsCam.transform.position, direction, out RaycastHit hit, range, whatIsEnemy))
            {
                if (hit.collider.CompareTag("Enemy"))
                {
                    IDamageable dmg = hit.collider.GetComponent<IDamageable>();
                    if (dmg != null)
                        dmg.TakeDamage(damage);
                }

                if (bulletHoleGraphic != null)
                    Instantiate(bulletHoleGraphic, hit.point, Quaternion.LookRotation(hit.normal));
            }
        }

        if (muzzleFlash != null)
            Instantiate(muzzleFlash, attackPoint.position, attackPoint.rotation);

        bulletsLeft--;

        Invoke(nameof(ResetShot), timeBetweenShooting);
    }

    private void ResetShot()
    {
        readyToShoot = true;
    }

    private void Reload()
    {
        reloading = true;
        Invoke(nameof(ReloadFinished), reloadTime);
    }

    private void ReloadFinished()
    {
        bulletsLeft = magazineSize;
        reloading = false;
    }

    private void UpdateAmmoUI()
    {
        if (ammoText != null)
            ammoText.SetText($"{bulletsLeft} / {magazineSize}");
    }
}
