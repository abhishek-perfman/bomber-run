using UnityEngine;

public class AAShell : MonoBehaviour
{
    public float speed = 22f;
    public float damageDistance = 2.5f;
    public float decoyDistance = 2.2f;
    public float lifeTime = 5f;

    private Transform planeTransform;
    private Transform specificTarget; // Can track a specific flare or plane
    private Vector3 moveDir;

    void Start()
    {
        // Find the plane
        Cannon plane = FindObjectOfType<Cannon>();
        if (plane != null)
        {
            planeTransform = plane.transform;
        }

        // Procedural Visual: A small red glowing sphere
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visual.transform.parent = this.transform;
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale = Vector3.one * 0.35f;
        Destroy(visual.GetComponent<Collider>()); // No colliders to prevent physics lag

        Renderer rend = visual.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material = ShaderHelper.CreateSafeMaterial(Color.red);
            ShaderHelper.SetSafeEmission(rend.material, new Color(1.5f, 0f, 0f)); // Glowing red
        }

        Destroy(gameObject, lifeTime);
    }

    public void SetSpecificTarget(Transform target, Vector3 fallbackDirection)
    {
        specificTarget = target;
        moveDir = fallbackDirection.normalized;
    }

    void Update()
    {
        // 1. Determine active tracking target
        Transform currentTarget = planeTransform;

        // Dynamic Flare Decoy Detection: if flares are in the air, the shell is attracted to the closest one in range
        if (DecoyFlare.activeFlares.Count > 0)
        {
            DecoyFlare closestFlare = null;
            float minDist = float.MaxValue;
            foreach (DecoyFlare flare in DecoyFlare.activeFlares)
            {
                if (flare != null)
                {
                    float d = Vector3.Distance(transform.position, flare.transform.position);
                    if (d < minDist)
                    {
                        minDist = d;
                        closestFlare = flare;
                    }
                }
            }
            // If closest flare is within deflection range (35 units), home in on it instead of the plane!
            if (closestFlare != null && minDist < 35f)
            {
                currentTarget = closestFlare.transform;
            }
        }
        else if (specificTarget != null)
        {
            currentTarget = specificTarget;
        }

        if (currentTarget != null)
            moveDir = (currentTarget.position - transform.position).normalized;

        // 2. Move towards target
        transform.Translate(moveDir * speed * Time.deltaTime, Space.World);

        // 3. Collision Checks via distance (highly reliable, bypasses physics collision layers)
        
        // A. Flare decoy check
        if (DecoyFlare.activeFlares.Count > 0)
        {
            foreach (DecoyFlare flare in DecoyFlare.activeFlares)
            {
                if (flare != null && Vector3.Distance(transform.position, flare.transform.position) < decoyDistance)
                {
                    // Decoy hit! Detonate early and do no damage
                    TriggerExplosionEffect(new Color(1f, 0.7f, 0.1f)); // Orange/Yellow sparks
                    Destroy(gameObject);
                    return;
                }
            }
        }

        // B. Plane damage check
        if (planeTransform != null && Vector3.Distance(transform.position, planeTransform.position) < damageDistance)
        {
            // Hit plane!
            Cannon planeScript = planeTransform.GetComponent<Cannon>();
            if (planeScript != null)
            {
                planeScript.TakeDamage();
            }

            TriggerExplosionEffect(Color.red); // Red damage sparks
            Destroy(gameObject);
            return;
        }
    }

    void TriggerExplosionEffect(Color color)
    {
        // Spawn a small puff of sparks to visually show detonation
        for (int i = 0; i < 6; i++)
        {
            GameObject spark = GameObject.CreatePrimitive(PrimitiveType.Cube);
            spark.transform.position = transform.position + Random.insideUnitSphere * 0.2f;
            spark.transform.localScale = Vector3.one * Random.Range(0.12f, 0.25f);
            Destroy(spark.GetComponent<Collider>());

            Renderer rend = spark.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material = ShaderHelper.CreateSafeMaterial(color);
                ShaderHelper.SetSafeEmission(rend.material, color * 1.5f);
            }

            Rigidbody rb = spark.AddComponent<Rigidbody>();
            if (rb != null)
            {
                // Use velocity instead of linearVelocity for compatibility
                rb.linearVelocity = Random.insideUnitSphere.normalized * Random.Range(3f, 7f);
                rb.useGravity = true;
            }

            Destroy(spark, Random.Range(0.2f, 0.4f));
        }
    }
}
