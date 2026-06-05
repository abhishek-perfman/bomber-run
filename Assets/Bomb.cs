using UnityEngine;

public class Bomb : MonoBehaviour
{
    [Header("Explosion Settings")]
    public float explosionForce = 1800f;
    public float explosionRadius = 8f;
    public float upwardModifier = 1.5f; // Makes bricks fly upward beautifully

    private bool exploded = false;

    void OnCollisionEnter(Collision collision)
    {
        // Explode on any collision (with a brick, plane, or floor)
        if (!exploded)
        {
            Explode();
        }
    }

    void Explode()
    {
        exploded = true;

        Vector3 explosionPos = transform.position;

        // 1. Apply explosive force to all rigidbodies in radius
        Collider[] colliders = Physics.OverlapSphere(explosionPos, explosionRadius);
        foreach (Collider hit in colliders)
        {
            Rigidbody rb = hit.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.AddExplosionForce(explosionForce, explosionPos, explosionRadius, upwardModifier);
            }
        }

        // 2. Procedural Explosion Visual Effect (glowing expanding sphere and debris sparks)
        CreateProceduralExplosionEffect(explosionPos);

        // 3. Play a satisfying explosion sound if an AudioSource exists
        AudioSource audio = GetComponent<AudioSource>();
        if (audio != null)
        {
            audio.Play();
        }

        // 4. Destroy the bomb itself
        Destroy(gameObject);
    }

    void CreateProceduralExplosionEffect(Vector3 pos)
    {
        // A. Create a central expanding fireball sphere
        GameObject fireball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        fireball.transform.position = pos;
        fireball.transform.localScale = Vector3.one * 0.5f;
        
        // Remove collider so it doesn't interfere with physics
        Destroy(fireball.GetComponent<Collider>());

        // Standard Shader configuration to support smooth colors and expansion
        Renderer rend = fireball.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material = new Material(Shader.Find("Standard"));
            rend.material.color = new Color(1f, 0.4f, 0f, 0.8f); // Bright Orange-Yellow
            
            // Set rendering mode to Transparent
            rend.material.SetFloat("_Mode", 3f);
            rend.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            rend.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            rend.material.SetInt("_ZWrite", 0);
            rend.material.DisableKeyword("_ALPHATEST_ON");
            rend.material.EnableKeyword("_ALPHABLEND_ON");
            rend.material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            rend.material.renderQueue = 3000;
        }

        // Add the expand-and-fade animation script
        fireball.AddComponent<ExplosionEffectHelper>().duration = 0.5f;

        // B. Spawn physical debris/sparks flying out dynamically
        for (int i = 0; i < 15; i++)
        {
            GameObject spark = GameObject.CreatePrimitive(PrimitiveType.Cube);
            spark.transform.position = pos + Random.insideUnitSphere * 0.5f;
            spark.transform.localScale = Vector3.one * Random.Range(0.2f, 0.4f);
            Destroy(spark.GetComponent<Collider>()); // No collision to avoid physics lag

            Renderer sparkRend = spark.GetComponent<Renderer>();
            if (sparkRend != null)
            {
                sparkRend.material = new Material(Shader.Find("Standard"));
                sparkRend.material.color = Color.Lerp(Color.yellow, Color.red, Random.value);
            }

            Rigidbody sparkRb = spark.AddComponent<Rigidbody>();
            if (sparkRb != null)
            {
                sparkRb.linearVelocity = (Random.insideUnitSphere.normalized + Vector3.up * 0.5f) * Random.Range(5f, 15f);
                sparkRb.useGravity = true;
            }

            Destroy(spark, Random.Range(0.4f, 0.8f)); // Destroy debris shortly after
        }
    }
}

// Simple helper to animate the procedural explosion fireball
public class ExplosionEffectHelper : MonoBehaviour
{
    public float duration = 0.5f;
    public float targetScale = 8f;
    private float elapsed = 0f;
    private Renderer rend;
    private Material mat;

    void Start()
    {
        rend = GetComponent<Renderer>();
        if (rend != null)
        {
            mat = rend.material;
        }
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float t = elapsed / duration;

        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        // Expand scale smoothly
        float currentScale = Mathf.Lerp(0.5f, targetScale, Mathf.Sin(t * Mathf.PI * 0.5f));
        transform.localScale = Vector3.one * currentScale;

        // Fade out color and alpha
        if (mat != null)
        {
            Color c = Color.Lerp(new Color(1f, 0.6f, 0.1f, 0.9f), new Color(0.2f, 0.2f, 0.2f, 0.0f), t);
            mat.color = c;
        }
    }
}
