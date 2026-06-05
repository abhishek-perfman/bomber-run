using UnityEngine;

public class Cannon : MonoBehaviour
{
    [Header("Original Prefab Reference (Do Not Delete)")]
    public GameObject cannonballPrefab; // This remains linked in Unity to your Sphere Prefab!
    public float shootForce = 2000f;     // Retained for scene compatibility

    [Header("Flight Controls")]
    public float flySpeed = 12f;
    public float turnSpeed = 65f;
    public float tiltAmount = 35f;

    [Header("Weapons")]
    public float bombReloadTime = 0.4f;

    private CastleBuilder castleBuilder;
    private GameObject visualPlane;
    private GameObject propeller;
    private float lastBombTime = 0f;
    private int bombsDropped = 0;
    private float currentRoll = 0f;
    private bool levelFinished = false;
    private bool levelFailed = false;
    private bool isDowned = false;

    [Header("Tactical Settings")]
    public int maxHealth = 3;
    private int currentHealth;
    public int maxFlares = 25;
    private int flaresLeft;
    private float lastFlareTime = 0f;
    public float flareReloadTime = 0.4f;
    private bool isBombCharging = false;
    private AudioClip warningBeepClip;
    private AudioClip rwrAlertClip;
    private float lastRwrAlertTime = 0f;
    private AudioClip bombReleaseClip;
    private AudioSource audioSource;

    [Header("Zoom Settings")]
    public float minFOV = 20f;
    public float maxFOV = 55f;
    private float targetFOV = 55f;
    private bool isZoomed = false;
    private GameObject targetReticle;

    public float normalPitch = 30f;
    public float zoomPitch = 90f;
    private float currentPitch = 30f;
    private float zoomProgress = 0f;

    private bool isUTurning = false;

    void Start()
    {
        // 1. Find the compound builder in the scene dynamically
        castleBuilder = FindObjectOfType<CastleBuilder>();

        // 2. Set the initial flight altitude (Y = 16) and position (behind the compound, further out)
        transform.position = new Vector3(0, 16f, -48f);
        // Start camera at normal flight pitch (30 degrees looking forward/down)
        transform.rotation = Quaternion.Euler(normalPitch, 0f, 0f);

        // Initialize tactical stats and settings
        currentHealth = maxHealth;
        flaresLeft = maxFlares;

        // Initialize target FOV and ensure the plane visuals aren't clipped by near clip plane settings
        Camera cam = GetComponent<Camera>();
        if (cam != null)
        {
            cam.fieldOfView = maxFOV;
            targetFOV = maxFOV;
            cam.nearClipPlane = 0.1f;
        }

        // Setup audio components
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        warningBeepClip = CreateWarningBeep();
        rwrAlertClip = CreateRwrAlertSound();
        bombReleaseClip = CreateBombReleaseSound();

        // 3. Spawns our procedurally designed, gorgeous visual plane!
        CreatePlaneVisuals();

        // 4. Create the 3D targeting reticle
        CreateTargetReticle();
    }

    void Update()
    {
        // R key restarts at any time
        if (Input.GetKeyDown(KeyCode.R))
        {
            RestartLevel();
        }

        if (levelFinished)
        {
            return;
        }

        // --- 1. Interactive Zoom Control (E Key Toggles, Mouse Scroll Adjusts) ---
        if (Input.GetKeyDown(KeyCode.E))
        {
            isZoomed = !isZoomed;
            targetFOV = isZoomed ? minFOV : maxFOV;
        }

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            targetFOV += -scroll * 20f;
            targetFOV = Mathf.Clamp(targetFOV, minFOV, maxFOV);
            isZoomed = targetFOV < (minFOV + maxFOV) / 2f;
        }

        // Interpolate zoom progression
        float targetProgress = isZoomed ? 1f : 0f;
        zoomProgress = Mathf.MoveTowards(zoomProgress, targetProgress, Time.deltaTime * 3.5f);

        Camera camComponent = GetComponent<Camera>();
        if (camComponent != null)
        {
            camComponent.fieldOfView = Mathf.Lerp(maxFOV, minFOV, zoomProgress);
        }

        // Calculate dynamic pitch based on zoom progress
        currentPitch = Mathf.Lerp(normalPitch, zoomPitch, zoomProgress);
        transform.rotation = Quaternion.Euler(currentPitch, transform.rotation.eulerAngles.y, 0f);

        // Adjust visual plane local position based on current zoom progress so it doesn't get hidden during zoom
        if (visualPlane != null)
        {
            float localY = Mathf.Lerp(-2.2f, -0.8f, zoomProgress);
            float localZ = Mathf.Lerp(9.0f, 6.0f, zoomProgress);
            visualPlane.transform.localPosition = new Vector3(0f, localY, localZ);
        }

        // --- 2. Flare Countermeasure (Q Key) ---
        if (Input.GetKeyDown(KeyCode.Q) && flaresLeft > 0 && Time.time > lastFlareTime + flareReloadTime)
        {
            DropFlare();
        }

        // --- 3. Flight Control (WASD / Arrows) & Automatic U-Turn Boundary ---
        float yawInput = Input.GetAxis("Horizontal");   // A/D or Left/Right Arrow

        // Check flat distance boundary (55 units from castle keep)
        Vector3 flatPos = new Vector3(transform.position.x, 0f, transform.position.z);
        bool reachedBoundary = flatPos.magnitude > 55f;

        // Initiate U-turn if requested manually or if boundary is reached automatically
        if (!isUTurning && (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S) || reachedBoundary))
        {
            StartCoroutine(ExecuteUTurn());
        }

        // Only allow manual control if not currently executing a U-turn
        if (!isUTurning)
        {
            // Decouple horizontal movement from camera pitch using Y-yaw heading
            Vector3 horizontalForward = GetHorizontalForward();

            // Move forward constantly in the horizontal direction
            transform.Translate(horizontalForward * flySpeed * Time.deltaTime, Space.World);

            // Turn left/right (Yaw)
            transform.Rotate(Vector3.up * yawInput * turnSpeed * Time.deltaTime, Space.World);

            // Up Arrow / W key climbs smoothly
            if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W))
            {
                float newY = transform.position.y + 6f * Time.deltaTime;
                newY = Mathf.Clamp(newY, 6f, 25f); // Keep between 6 and 25 units high
                transform.position = new Vector3(transform.position.x, newY, transform.position.z);
            }

            // --- 4. Dynamic Plane Banking & Propeller Spinning ---
            if (visualPlane != null)
            {
                // Tilt plane model local rotation based on horizontal turn input
                currentRoll = Mathf.Lerp(currentRoll, -yawInput * tiltAmount, Time.deltaTime * 5f);
                // Correct orientation relative to camera: align flat relative to ground
                visualPlane.transform.localRotation = Quaternion.Euler(-currentPitch, 0f, currentRoll);
            }
        }

        if (propeller != null)
        {
            // Spin the propeller blades rapidly
            propeller.transform.Rotate(Vector3.forward * 1200f * Time.deltaTime);
        }

        // --- 4. Bomb Dropping (Space or Left Click with charge beep warning) ---
        if (!isUTurning && bombsDropped < 5 && !isDowned && !isBombCharging && (Input.GetButtonDown("Fire1") || Input.GetKeyDown(KeyCode.Space)) && Time.time > lastBombTime + bombReloadTime)
        {
            StartCoroutine(ChargeAndDropBomb());
        }

        // --- 5. Check Victory & Failure Conditions ---
        if (castleBuilder != null)
        {
            float percentage = castleBuilder.GetDestructionPercentage();
            if (percentage >= 80f)
            {
                levelFinished = true;
                levelFailed = false;
            }
            else if ((bombsDropped >= 5 || isDowned) && !levelFinished)
            {
                // Wait for active bombs to settle unless plane crashed
                Bomb[] activeBombs = FindObjectsOfType<Bomb>();
                if (activeBombs.Length == 0 || isDowned)
                {
                    levelFailed = true;
                }
            }
        }

        // --- Lock Warning Alert for AA Guns (RWR) ---
        CheckAAGunThreats();

        // --- 6. Update 3D Targeting Reticle ---
        if (targetReticle != null)
        {
            if (isZoomed && !isDowned && !levelFinished && !levelFailed)
            {
                targetReticle.SetActive(true);

                // Calculate bomb landing position incorporating velocity + gravity
                float h = transform.position.y;
                float g = Mathf.Abs(Physics.gravity.y);
                if (g < 0.1f) g = 9.81f;
                float flightTime = Mathf.Sqrt(2f * h / g);

                Vector3 forwardDir = GetHorizontalForward();
                Vector3 projectedLanding = transform.position + forwardDir * (flySpeed * flightTime);
                projectedLanding.y = 30f; // Cast downward from above the map

                RaycastHit hit;
                if (Physics.Raycast(projectedLanding, Vector3.down, out hit, 50f))
                {
                    targetReticle.transform.position = hit.point + new Vector3(0f, 0.05f, 0f);
                }
                else
                {
                    targetReticle.transform.position = new Vector3(projectedLanding.x, 0.05f, projectedLanding.z);
                }
            }
            else
            {
                targetReticle.SetActive(false);
            }
        }
    }

    System.Collections.IEnumerator ExecuteUTurn()
    {
        isUTurning = true;
        float duration = 1.2f; // Smooth turn over 1.2 seconds
        float elapsed = 0f;

        Quaternion startRotation = transform.rotation;

        // Calculate direction pointing directly back to the castle in the center
        Vector3 dirToCastle = (Vector3.zero - transform.position);
        dirToCastle.y = 0f;
        
        // If we are already near the center, just face exactly backwards
        if (dirToCastle.sqrMagnitude < 2f)
        {
            dirToCastle = -transform.forward;
            dirToCastle.y = 0f;
        }
        dirToCastle.Normalize();

        Quaternion targetRotation = Quaternion.LookRotation(dirToCastle);
        // Maintain current pitch during the U-turn
        targetRotation = Quaternion.Euler(currentPitch, targetRotation.eulerAngles.y, 0f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            // Continue moving forward in the horizontal direction during the U-turn
            Vector3 horizontalForward = GetHorizontalForward();
            transform.Translate(horizontalForward * flySpeed * Time.deltaTime, Space.World);

            // Interpolate rotation smoothly
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, smoothT);

            // Gorgeous dynamic bank roll (tilting into the turn, then leveling off)
            if (visualPlane != null)
            {
                float rollAngle = Mathf.Sin(smoothT * Mathf.PI) * -65f;
                // Preserve current dynamic pitch orientation while banking
                visualPlane.transform.localRotation = Quaternion.Euler(-currentPitch, 0f, rollAngle);
            }

            yield return null;
        }

        isUTurning = false;
    }

    Vector3 GetHorizontalForward()
    {
        float yawRad = transform.eulerAngles.y * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(yawRad), 0f, Mathf.Cos(yawRad));
    }

    void DropBomb(Vector3 forwardDir)
    {
        lastBombTime = Time.time;
        bombsDropped++;

        if (audioSource != null && bombReleaseClip != null)
        {
            audioSource.PlayOneShot(bombReleaseClip);
        }

        // Spawn bomb slightly below the procedural plane
        Vector3 spawnPos = transform.position + transform.forward * 6f - transform.up * 2f;
        if (visualPlane != null)
        {
            spawnPos = visualPlane.transform.position - new Vector3(0, 0.5f, 0);
        }

        GameObject bomb = Instantiate(cannonballPrefab, spawnPos, Quaternion.identity);

        // Configure physical bombing dynamics
        Rigidbody bombRb = bomb.GetComponent<Rigidbody>();
        if (bombRb != null)
        {
            // Inherit the plane's forward velocity and apply a tiny downward push (using velocity for compatibility)
            bombRb.linearVelocity = forwardDir * flySpeed + Vector3.down * 1.5f;
            
            // Attach/Configure the Bomb script to cause physical explosions!
            Bomb bombComp = bomb.GetComponent<Bomb>();
            if (bombComp == null)
            {
                bombComp = bomb.AddComponent<Bomb>();
            }
            bombComp.explosionForce = 1800f;
            bombComp.explosionRadius = 8f;
        }
    }

    public void TakeDamage()
    {
        if (levelFinished || levelFailed || isDowned) return;

        currentHealth--;
        if (currentHealth <= 0)
        {
            currentHealth = 0;
            levelFailed = true;
            isDowned = true;

            // Trigger visual crash plunge
            if (visualPlane != null)
            {
                visualPlane.transform.parent = null;
                Rigidbody planeRb = visualPlane.AddComponent<Rigidbody>();
                if (planeRb != null)
                {
                    planeRb.linearVelocity = GetHorizontalForward() * flySpeed + Vector3.down * 2.5f;
                    planeRb.angularVelocity = new Vector3(Random.Range(-5f, 5f), Random.Range(-5f, 5f), Random.Range(-5f, 5f));
                }
                Destroy(visualPlane, 4.5f);
            }
        }
    }

    void DropFlare()
    {
        flaresLeft--;
        lastFlareTime = Time.time;

        Vector3 spawnPos = transform.position - new Vector3(0f, 1.5f, 0f);
        if (visualPlane != null)
        {
            spawnPos = visualPlane.transform.position - new Vector3(0f, 0.4f, 0f);
        }

        GameObject flareObj = new GameObject("Decoy_Flare");
        flareObj.transform.position = spawnPos;
        flareObj.AddComponent<DecoyFlare>();
    }

    void CreateTargetReticle()
    {
        targetReticle = new GameObject("TargetReticle");

        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.transform.parent = targetReticle.transform;
        ring.transform.localPosition = Vector3.zero;
        ring.transform.localRotation = Quaternion.identity;
        ring.transform.localScale = new Vector3(2.5f, 0.02f, 2.5f);
        Destroy(ring.GetComponent<Collider>());

        GameObject dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        dot.transform.parent = targetReticle.transform;
        dot.transform.localPosition = Vector3.zero;
        dot.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
        Destroy(dot.GetComponent<Collider>());

        Material reticleMat = ShaderHelper.CreateSafeMaterial(new Color(0f, 1f, 0.2f, 0.8f));
        ShaderHelper.SetSafeEmission(reticleMat, new Color(0f, 0.8f, 0.1f));

        ring.GetComponent<Renderer>().material = reticleMat;
        dot.GetComponent<Renderer>().material = reticleMat;

        targetReticle.SetActive(false);
    }

    void CreatePlaneVisuals()
    {
        // Create parent container for plane visual parts, offset in front of the camera view
        visualPlane = new GameObject("PlaneVisuals");
        visualPlane.transform.parent = this.transform;
        visualPlane.transform.localPosition = new Vector3(0f, -2.2f, 9.0f);
        // Correct default plane orientation relative to camera: -90f pitch to keep it flat
        visualPlane.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

        // Custom Harmonious Materials (Vibrant Blue Design Theme)
        Material fuselageMat = ShaderHelper.CreateSafeMaterial(new Color(0.04f, 0.22f, 0.65f), 0.8f);
        Material wingMat = ShaderHelper.CreateSafeMaterial(new Color(0.25f, 0.72f, 1.0f), 0.6f);
        Material metalMat = ShaderHelper.CreateSafeMaterial(new Color(0.7f, 0.7f, 0.72f), 0.5f);

        // A. Fuselage (Central Body)
        GameObject fuselage = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        fuselage.transform.parent = visualPlane.transform;
        fuselage.transform.localPosition = Vector3.zero;
        fuselage.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // Face along Z axis
        fuselage.transform.localScale = new Vector3(0.7f, 1.4f, 0.7f); // Stretched cylinder
        Destroy(fuselage.GetComponent<Collider>()); // Visuals don't need collisions
        fuselage.GetComponent<Renderer>().material = fuselageMat;

        // B. Main Wing (Large horizontal wing)
        GameObject wing = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wing.transform.parent = visualPlane.transform;
        wing.transform.localPosition = new Vector3(0f, 0f, 0.2f);
        wing.transform.localRotation = Quaternion.identity;
        wing.transform.localScale = new Vector3(5.5f, 0.08f, 0.9f); // Long wing
        Destroy(wing.GetComponent<Collider>());
        wing.GetComponent<Renderer>().material = wingMat;

        // C. Tail Wing (Small horizontal stabilizer)
        GameObject tailWing = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tailWing.transform.parent = visualPlane.transform;
        tailWing.transform.localPosition = new Vector3(0f, 0.2f, -1.1f);
        tailWing.transform.localRotation = Quaternion.identity;
        tailWing.transform.localScale = new Vector3(1.8f, 0.08f, 0.45f);
        Destroy(tailWing.GetComponent<Collider>());
        tailWing.GetComponent<Renderer>().material = wingMat;

        // D. Vertical Stabilizer (Tail Fin)
        GameObject rudder = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rudder.transform.parent = visualPlane.transform;
        rudder.transform.localPosition = new Vector3(0f, 0.6f, -1.1f);
        rudder.transform.localRotation = Quaternion.identity;
        rudder.transform.localScale = new Vector3(0.08f, 0.8f, 0.45f);
        Destroy(rudder.GetComponent<Collider>());
        rudder.GetComponent<Renderer>().material = fuselageMat;

        // E. Propeller Engine Hub
        GameObject hub = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        hub.transform.parent = visualPlane.transform;
        hub.transform.localPosition = new Vector3(0f, 0f, 1.45f);
        hub.transform.localRotation = Quaternion.identity;
        hub.transform.localScale = new Vector3(0.45f, 0.45f, 0.45f);
        Destroy(hub.GetComponent<Collider>());
        hub.GetComponent<Renderer>().material = metalMat;

        // F. Propeller Blades (Spins in Update)
        propeller = GameObject.CreatePrimitive(PrimitiveType.Cube);
        propeller.transform.parent = visualPlane.transform;
        propeller.transform.localPosition = new Vector3(0f, 0f, 1.5f);
        propeller.transform.localRotation = Quaternion.identity;
        propeller.transform.localScale = new Vector3(2.2f, 0.16f, 0.04f); // Long thin blades
        Destroy(propeller.GetComponent<Collider>());
        propeller.GetComponent<Renderer>().material = metalMat;
    }

    AudioClip CreateWarningBeep()
    {
        int samplerate = 44100;
        float duration = 0.22f; // Short double beep warning
        int sampleCount = (int)(samplerate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / samplerate;
            float wave = 0f;
            // First beep: 0s to 0.08s
            if (t < 0.08f)
            {
                wave = Mathf.Sin(2f * Mathf.PI * 980f * t) * 0.35f;
            }
            // Second beep: 0.11s to 0.19s
            else if (t > 0.11f && t < 0.19f)
            {
                wave = Mathf.Sin(2f * Mathf.PI * 980f * (t - 0.11f)) * 0.35f;
            }
            samples[i] = wave * (1f - (t / duration)); // Linear decay
        }

        AudioClip clip = AudioClip.Create("PreFireBeep", sampleCount, 1, samplerate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    System.Collections.IEnumerator ChargeAndDropBomb()
    {
        isBombCharging = true;

        if (audioSource != null && warningBeepClip != null)
        {
            audioSource.PlayOneShot(warningBeepClip);
        }

        // Wait 0.25 seconds charge delay
        yield return new WaitForSeconds(0.25f);

        if (!isDowned && !levelFailed && !levelFinished)
        {
            Vector3 horizontalForward = GetHorizontalForward();
            DropBomb(horizontalForward);
            lastBombTime = Time.time;
        }

        isBombCharging = false;
    }

    void RestartLevel()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
        );
    }

    void OnGUI()
    {
        // HUD Container Box Style
        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.normal.background = MakeTex(2, 2, new Color(0.1f, 0.15f, 0.25f, 0.85f)); // Glassmorphism backdrop

        // Font Style Configuration
        GUIStyle textStyle = new GUIStyle();
        textStyle.fontSize = 20;
        textStyle.normal.textColor = Color.white;
        textStyle.fontStyle = FontStyle.Bold;

        GUIStyle shadowStyle = new GUIStyle(textStyle);
        shadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.7f);

        // Draw main HUD backdrop panel (expanded slightly for health/flares)
        GUI.Box(new Rect(15, 15, 380, 175), "", boxStyle);

        float percentage = 0f;
        if (castleBuilder != null)
        {
            percentage = castleBuilder.GetDestructionPercentage();
        }

        // Demolition Title
        GUI.Label(new Rect(27, 27, 340, 30), "COMPOUND DEMOLITION SIM", shadowStyle);
        GUI.Label(new Rect(25, 25, 340, 30), "COMPOUND DEMOLITION SIM", textStyle);

        // Score / Destruction level
        string statsStr = string.Format("Compound Demolished: {0:F1}%", percentage);
        GUI.Label(new Rect(27, 62, 340, 30), statsStr, shadowStyle);
        
        GUIStyle percentStyle = new GUIStyle(textStyle);
        percentStyle.normal.textColor = percentage >= 80f ? Color.green : (percentage >= 40f ? Color.yellow : new Color(0.2f, 0.85f, 1f));
        GUI.Label(new Rect(25, 60, 340, 30), statsStr, percentStyle);

        // Bomb counter with 5 Ammo Limit
        string bombsStr = string.Format("Bombs: {0} / 5", bombsDropped);
        if (bombsDropped >= 5)
        {
            bombsStr += " (OUT OF AMMO)";
        }
        GUI.Label(new Rect(27, 92, 340, 30), bombsStr, shadowStyle);

        GUIStyle ammoStyle = new GUIStyle(textStyle);
        if (bombsDropped >= 5)
        {
            ammoStyle.normal.textColor = new Color(1f, 0.2f, 0.2f);
        }
        else if (bombsDropped >= 4)
        {
            ammoStyle.normal.textColor = Color.yellow;
        }
        else
        {
            ammoStyle.normal.textColor = Color.white;
        }
        GUI.Label(new Rect(25, 90, 340, 30), bombsStr, ammoStyle);

        // Flares counter
        string flaresStr = string.Format("Flares: {0} / 25", flaresLeft);
        GUI.Label(new Rect(27, 122, 340, 30), flaresStr, shadowStyle);
        GUIStyle flareHUDStyle = new GUIStyle(textStyle);
        flareHUDStyle.normal.textColor = flaresLeft <= 5 ? Color.yellow : Color.white;
        GUI.Label(new Rect(25, 120, 340, 30), flaresStr, flareHUDStyle);

        // Armor integrity / Health bar
        string armorStr = "Armor: ";
        for (int i = 0; i < maxHealth; i++)
        {
            if (i < currentHealth)
                armorStr += "█";
            else
                armorStr += "░";
        }
        armorStr += string.Format(" ({0}/{1})", currentHealth, maxHealth);
        GUI.Label(new Rect(27, 152, 340, 30), armorStr, shadowStyle);
        GUIStyle armorColorStyle = new GUIStyle(textStyle);
        armorColorStyle.normal.textColor = currentHealth == 3 ? Color.green : (currentHealth == 2 ? Color.yellow : Color.red);
        GUI.Label(new Rect(25, 150, 340, 30), armorStr, armorColorStyle);

        float screenW = Screen.width;
        float screenH = Screen.height;

        // Bottom horizontal instructions bar
        string helpStr = "A/D: Steer | W: Climb | S: U-Turn | E: Zoom | Q: Flares | Space: Bomb";
        GUIStyle helpStyle = new GUIStyle(textStyle);
        helpStyle.fontSize = 13;
        helpStyle.alignment = TextAnchor.MiddleCenter;
        helpStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
        GUI.Box(new Rect(screenW / 2f - 290, screenH - 45, 580, 30), "", boxStyle);
        GUI.Label(new Rect(screenW / 2f - 288, screenH - 43, 580, 30), helpStr, new GUIStyle(helpStyle) { normal = { textColor = Color.black } });
        GUI.Label(new Rect(screenW / 2f - 290, screenH - 45, 580, 30), helpStr, helpStyle);

        // Zoom Crosshair Overlay
        if (isZoomed && !levelFailed && !levelFinished && !isDowned)
        {
            float crosshairSize = 32f;
            float thickness = 2f;
            Color crossColor = new Color(0f, 1f, 0.2f, 0.9f);
            Texture2D crossTex = MakeTex(2, 2, crossColor);
            float cX = screenW / 2f;
            float cY = screenH / 2f;
            GUI.DrawTexture(new Rect(cX - crosshairSize / 2f, cY - thickness / 2f, crosshairSize, thickness), crossTex);
            GUI.DrawTexture(new Rect(cX - thickness / 2f, cY - crosshairSize / 2f, thickness, crosshairSize), crossTex);
            GUI.DrawTexture(new Rect(cX - 4f, cY - 4f, 8f, 1f), crossTex);
            GUI.DrawTexture(new Rect(cX - 4f, cY + 3f, 8f, 1f), crossTex);
            GUI.DrawTexture(new Rect(cX - 4f, cY - 4f, 1f, 8f), crossTex);
            GUI.DrawTexture(new Rect(cX + 3f, cY - 4f, 1f, 8f), crossTex);
        }

        // Victory Screen Overlay
        if (levelFinished)
        {
            GUIStyle winTitleStyle = new GUIStyle();
            winTitleStyle.fontSize = 42;
            winTitleStyle.normal.textColor = new Color(0f, 1f, 0.4f);
            winTitleStyle.fontStyle = FontStyle.Bold;
            winTitleStyle.alignment = TextAnchor.MiddleCenter;

            GUIStyle winTitleShadow = new GUIStyle(winTitleStyle);
            winTitleShadow.normal.textColor = Color.black;

            GUIStyle winSubStyle = new GUIStyle(textStyle);
            winSubStyle.fontSize = 18;
            winSubStyle.alignment = TextAnchor.MiddleCenter;

            GUI.Box(new Rect(screenW / 2f - 240, screenH / 2f - 95, 480, 190), "", boxStyle);
            GUI.Label(new Rect(screenW / 2f - 238, screenH / 2f - 78, 480, 80), "COMPOUND DESTROYED!", winTitleShadow);
            GUI.Label(new Rect(screenW / 2f - 240, screenH / 2f - 80, 480, 80), "COMPOUND DESTROYED!", winTitleStyle);

            GUI.Label(new Rect(screenW / 2f - 240, screenH / 2f + 15, 480, 30), string.Format("Success with {0} bombs dropped and {1} flares left!", bombsDropped, flaresLeft), winSubStyle);
            GUI.Label(new Rect(screenW / 2f - 240, screenH / 2f + 45, 480, 30), "Press R to start new flight.", winSubStyle);
        }
        // Failure Screen Overlay
        else if (levelFailed)
        {
            GUIStyle failTitleStyle = new GUIStyle();
            failTitleStyle.fontSize = 42;
            failTitleStyle.normal.textColor = new Color(1f, 0.2f, 0.2f);
            failTitleStyle.fontStyle = FontStyle.Bold;
            failTitleStyle.alignment = TextAnchor.MiddleCenter;

            GUIStyle failTitleShadow = new GUIStyle(failTitleStyle);
            failTitleShadow.normal.textColor = Color.black;

            GUIStyle failSubStyle = new GUIStyle(textStyle);
            failSubStyle.fontSize = 18;
            failSubStyle.alignment = TextAnchor.MiddleCenter;

            GUI.Box(new Rect(screenW / 2f - 240, screenH / 2f - 95, 480, 190), "", boxStyle);
            
            string failText = isDowned ? "PLANE SHOT DOWN!" : "OUT OF AMMO! FAILED";
            GUI.Label(new Rect(screenW / 2f - 238, screenH / 2f - 78, 480, 80), failText, failTitleShadow);
            GUI.Label(new Rect(screenW / 2f - 240, screenH / 2f - 80, 480, 80), failText, failTitleStyle);

            GUI.Label(new Rect(screenW / 2f - 240, screenH / 2f + 15, 480, 30), string.Format("Final Demolition: {0:F1}% (Goal: 80%)", percentage), failSubStyle);
            GUI.Label(new Rect(screenW / 2f - 240, screenH / 2f + 45, 480, 30), "Press R to try again.", failSubStyle);
        }
    }

    // Helper to generate dynamic colored background texture for premium HUD look
    private Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; ++i)
        {
            pix[i] = col;
        }
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }

    void CheckAAGunThreats()
    {
        if (levelFinished || levelFailed || isDowned) return;

        bool threatDetected = false;
        AntiAircraftGun[] guns = FindObjectsOfType<AntiAircraftGun>();
        
        foreach (AntiAircraftGun gun in guns)
        {
            if (gun != null && gun.IsAboutToFireAt(this.transform, 1.5f))
            {
                threatDetected = true;
                break;
            }
        }

        if (threatDetected)
        {
            // Play rapid alert warning beeps (every 0.25 seconds)
            if (Time.time > lastRwrAlertTime + 0.25f)
            {
                if (audioSource != null && rwrAlertClip != null)
                {
                    audioSource.PlayOneShot(rwrAlertClip);
                }
                lastRwrAlertTime = Time.time;
            }
        }
    }

    AudioClip CreateRwrAlertSound()
    {
        int samplerate = 44100;
        float duration = 0.1f; // Short alert beep
        int sampleCount = (int)(samplerate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / samplerate;
            // 1200 Hz warning frequency
            float wave = Mathf.Sin(2f * Mathf.PI * 1200f * t) * 0.3f;
            samples[i] = wave * (1f - (t / duration)); // Linear decay
        }

        AudioClip clip = AudioClip.Create("RwrAlertBeep", sampleCount, 1, samplerate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    AudioClip CreateBombReleaseSound()
    {
        int samplerate = 44100;
        float duration = 0.35f;
        int sampleCount = (int)(samplerate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / samplerate;
            // A combination of white noise and a low frequency pitch drop (whosh + clunk)
            float pitchDrop = Mathf.Lerp(250f, 80f, t / duration);
            float sine = Mathf.Sin(2f * Mathf.PI * pitchDrop * t);
            float noise = (Random.value * 2f - 1f) * 0.15f;
            
            samples[i] = (sine + noise) * 0.4f * (1f - (t / duration)); // Linear decay
        }

        AudioClip clip = AudioClip.Create("BombRelease", sampleCount, 1, samplerate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}

public static class ShaderHelper
{
    public static Shader FindSafeShader()
    {
        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        if (s == null) s = Shader.Find("Standard");
        if (s == null) s = Shader.Find("Diffuse");
        return s;
    }

    public static Material CreateSafeMaterial(Color color, float smoothness = 0.5f)
    {
        Shader shader = FindSafeShader();
        Material mat = (shader != null) ? new Material(shader) : new Material(Shader.Find("Hidden/InternalErrorShader"));
        
        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", color);
        }
        else if (mat.HasProperty("_Color"))
        {
            mat.SetColor("_Color", color);
        }
        else
        {
            mat.color = color;
        }

        if (mat.HasProperty("_Smoothness"))
        {
            mat.SetFloat("_Smoothness", smoothness);
        }
        else if (mat.HasProperty("_Glossiness"))
        {
            mat.SetFloat("_Glossiness", smoothness);
        }

        return mat;
    }

    public static void SetSafeEmission(Material mat, Color emissionColor)
    {
        mat.EnableKeyword("_EMISSION");
        if (mat.HasProperty("_EmissionColor"))
        {
            mat.SetColor("_EmissionColor", emissionColor);
        }
    }
}
