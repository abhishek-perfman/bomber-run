using UnityEngine;
using System.Collections.Generic;

public class DecoyFlare : MonoBehaviour
{
    public static List<DecoyFlare> activeFlares = new List<DecoyFlare>();

    public float fallSpeed = 3.5f;
    public float driftAmount = 1.2f;
    public float lifeTime = 4.5f;

    private Vector3 driftDir;

    void Start()
    {
        activeFlares.Add(this);
        driftDir = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized;

        // Visual: A bright yellow-white sphere
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visual.transform.parent = this.transform;
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale = Vector3.one * 0.4f;
        Destroy(visual.GetComponent<Collider>()); // No colliders to prevent physics lag

        Renderer rend = visual.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material = ShaderHelper.CreateSafeMaterial(new Color(1f, 1f, 0.8f));
            ShaderHelper.SetSafeEmission(rend.material, new Color(1f, 0.9f, 0.5f));
        }

        Destroy(gameObject, lifeTime);
    }

    void OnDestroy()
    {
        activeFlares.Remove(this);
    }

    void Update()
    {
        // Move slowly downward and drift slightly sideways
        transform.Translate((Vector3.down * fallSpeed + driftDir * driftAmount) * Time.deltaTime, Space.World);

        // Generate spark particles procedurally
        if (Random.value < 0.35f)
        {
            SpawnSpark();
        }
    }

    void SpawnSpark()
    {
        GameObject spark = GameObject.CreatePrimitive(PrimitiveType.Cube);
        spark.transform.position = transform.position + Random.insideUnitSphere * 0.2f;
        spark.transform.localScale = Vector3.one * Random.Range(0.1f, 0.2f);
        Destroy(spark.GetComponent<Collider>());

        Renderer sparkRend = spark.GetComponent<Renderer>();
        if (sparkRend != null)
        {
            Color sparkCol = Color.Lerp(Color.yellow, new Color(1f, 0.5f, 0f), Random.value);
            sparkRend.material = ShaderHelper.CreateSafeMaterial(sparkCol);
            ShaderHelper.SetSafeEmission(sparkRend.material, sparkCol * 2f);
        }

        Rigidbody rb = spark.AddComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = (Random.insideUnitSphere.normalized + Vector3.up * 0.2f) * Random.Range(1f, 3f);
            rb.useGravity = true;
        }

        Destroy(spark, Random.Range(0.3f, 0.6f));
    }
}
