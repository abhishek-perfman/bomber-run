using UnityEngine;

public class AntiAircraftGun : MonoBehaviour
{
    public float fireRate = 5.0f;
    public float targetRange = 60f;
    public float flareAttractRange = 38f;

    private float lastFireTime = 0f;

    private Transform planeTransform;
    private GameObject turretHead;
    private GameObject barrel;

    void Start()
    {
        // Find plane in scene
        Cannon plane = FindObjectOfType<Cannon>();
        if (plane != null)
        {
            planeTransform = plane.transform;
        }

        // Procedural turret visual design
        // A. Turret Base (flat circular slab)
        GameObject turretBase = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        turretBase.transform.parent = this.transform;
        turretBase.transform.localPosition = new Vector3(0, 0.1f, 0);
        turretBase.transform.localScale = new Vector3(1.2f, 0.15f, 1.2f);
        Destroy(turretBase.GetComponent<Collider>());

        // B. Turret Head (rotatable cube box)
        turretHead = GameObject.CreatePrimitive(PrimitiveType.Cube);
        turretHead.transform.parent = this.transform;
        turretHead.transform.localPosition = new Vector3(0, 0.5f, 0);
        turretHead.transform.localScale = new Vector3(0.8f, 0.6f, 0.8f);
        Destroy(turretHead.GetComponent<Collider>());

        // C. Turret Barrel (pointing forward, child of Turret Head)
        barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        barrel.transform.parent = turretHead.transform;
        // Point along local Z-axis (forward) by tilting cylinder pitch 90 degrees
        barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        barrel.transform.localPosition = new Vector3(0f, 0f, 0.6f);
        barrel.transform.localScale = new Vector3(0.18f, 0.6f, 0.18f); // Long thin barrel
        Destroy(barrel.GetComponent<Collider>());

        // Standard Gunmetal Grey material
        Material gunMat = ShaderHelper.CreateSafeMaterial(new Color(0.18f, 0.20f, 0.24f), 0.2f);

        turretBase.GetComponent<Renderer>().material = gunMat;
        turretHead.GetComponent<Renderer>().material = gunMat;
        barrel.GetComponent<Renderer>().material = gunMat;

        // Spread fire start times slightly so not all turrets fire at the exact same millisecond
        lastFireTime = Time.time + Random.Range(0f, 0.7f);
    }

    void Update()
    {
        // 1. Determine active target
        Transform activeTarget = GetTarget();
        if (activeTarget == null) return;

        float dist = Vector3.Distance(transform.position, activeTarget.position);
        if (dist > targetRange) return;

        // 2. Rotate turret head to look at target
        Vector3 targetDirection = activeTarget.position - turretHead.transform.position;
        if (targetDirection != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(targetDirection);
            turretHead.transform.rotation = Quaternion.Slerp(turretHead.transform.rotation, targetRot, Time.deltaTime * 6f);
        }

        // 3. Firing mechanic (every 1 second)
        if (Time.time > lastFireTime + fireRate)
        {
            FireShell(activeTarget);
        }
    }

    public bool IsAboutToFireAt(Transform target, float warningTimeWindow)
    {
        if (GetTarget() != target) return false;

        float dist = Vector3.Distance(transform.position, target.position);
        if (dist > targetRange) return false;

        float timeToFire = (lastFireTime + fireRate) - Time.time;
        return (timeToFire > 0f && timeToFire <= warningTimeWindow);
    }

    public Transform GetTarget()
    {
        // Priority 1: Check active decoy flares in range
        Transform closestFlare = null;
        float minFlareDist = float.MaxValue;

        if (DecoyFlare.activeFlares.Count > 0)
        {
            foreach (DecoyFlare flare in DecoyFlare.activeFlares)
            {
                if (flare != null)
                {
                    float d = Vector3.Distance(transform.position, flare.transform.position);
                    if (d < flareAttractRange && d < minFlareDist)
                    {
                        minFlareDist = d;
                        closestFlare = flare.transform;
                    }
                }
            }
        }

        if (closestFlare != null)
        {
            return closestFlare;
        }

        // Priority 2: Standard target (the plane)
        return planeTransform;
    }

    void FireShell(Transform target)
    {
        lastFireTime = Time.time;

        // Spawn position: tip of the barrel
        Vector3 firePort = turretHead.transform.position + turretHead.transform.forward * 1.3f;

        GameObject shellObj = new GameObject("AA_Shell");
        shellObj.transform.position = firePort;

        AAShell shell = shellObj.AddComponent<AAShell>();
        shell.SetSpecificTarget(target, turretHead.transform.forward);
    }
}
