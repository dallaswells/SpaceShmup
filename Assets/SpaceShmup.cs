using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SpaceShmup : MonoBehaviour {
    public GameObject heroModel;
    public GameObject[] enemies;
    public Texture2D stars;

    class Ship {
        public Transform body;
        public int type, health;
        public float age, lane, nextShot;
    }

    class Shot {
        public Transform body;
        public Vector3 velocity;
        public bool hostile;
    }

    readonly List<Ship> ships = new List<Ship>();
    readonly List<Shot> shots = new List<Shot>();
    readonly Transform[] backgrounds = new Transform[2];
    Transform hero;
    Material blue, red;
    float edge, spawnTime, fireTime, safeUntil, pulseUntil, nextPulse, elapsed;
    int spawned, score, shields = 4, charge;
    bool started;
    AudioSource effects, music;
    AudioClip playerGun, enemyLaser, enemyDeath, overdriveReady, overdriveActive, playerDeath, playerTakeDamage;

    void InitializeAudio() {
        if (FindObjectOfType<AudioListener>() == null)
            Camera.main.gameObject.AddComponent<AudioListener>();
        effects = gameObject.AddComponent<AudioSource>();
        effects.playOnAwake = false;
        effects.spatialBlend = 0;
        music = gameObject.AddComponent<AudioSource>();
        music.playOnAwake = false;
        music.spatialBlend = 0;
        music.loop = true;
        music.volume = 0.3f;
        music.clip = Resources.Load<AudioClip>("Audio/backgroundtheme");
        playerGun = Resources.Load<AudioClip>("Audio/PLAYERGUN");
        enemyLaser = Resources.Load<AudioClip>("Audio/enemylazer");
        enemyDeath = Resources.Load<AudioClip>("Audio/enemydeath");
        overdriveReady = Resources.Load<AudioClip>("Audio/overdriveready");
        overdriveActive = Resources.Load<AudioClip>("Audio/overdriveactive");
        playerDeath = Resources.Load<AudioClip>("Audio/playerdeath");
        playerTakeDamage = Resources.Load<AudioClip>("Audio/playertakedamage");
        music.Play();
    }

    void Start() {
        InitializeAudio();
        edge = Camera.main.aspect * 10;
        hero = Instantiate(heroModel, new Vector3(0,-7,0), Quaternion.identity).transform;
        SizeModel(hero, 1.8f);
        var starMaterial = new Material(Shader.Find("Unlit/Texture")) { mainTexture = stars };
        for (int i=0; i<2; i++) {
            var tile = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(tile.GetComponent<Collider>());
            tile.name = "Scrolling starfield";
            tile.GetComponent<Renderer>().sharedMaterial = starMaterial;
            tile.transform.localScale = new Vector3(2 * edge,20,1);
            tile.transform.position = new Vector3(0,20*i,12);
            backgrounds[i] = tile.transform;
        }
        blue = ShotMaterial(new Color(.18f,1,.95f));
        red = ShotMaterial(new Color(1,.35f,.17f));
    }

    static Material ShotMaterial(Color color) {
        return new Material(Shader.Find("Unlit/Color")) { color = color };
    }

    static void SizeModel(Transform model, float size) {
        var renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer item in renderers) bounds.Encapsulate(item.bounds);
        model.localScale *= size / Mathf.Max(.01f, bounds.size.x, bounds.size.y, bounds.size.z);
    }

    static int Axis(KeyCode a, KeyCode b, KeyCode c, KeyCode d) {
        return (Input.GetKey(c) || Input.GetKey(d) ? 1 : 0) -
            (Input.GetKey(a) || Input.GetKey(b) ? 1 : 0);
    }

    void Update() {
        float dt = Time.deltaTime;
        foreach (Transform tile in backgrounds) {
            tile.position += Vector3.down * (2 * dt);
            if (tile.position.y <= -20) tile.position += Vector3.up * 40;
        }
        if (!started) {
            started = Input.GetKeyDown(KeyCode.Return);
            return;
        }
        if (shields <= 0) {
            if (Input.GetKeyDown(KeyCode.R)) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            return;
        }

        elapsed += dt;
        Vector3 move = new Vector3(Axis(KeyCode.A, KeyCode.LeftArrow, KeyCode.D, KeyCode.RightArrow), Axis(KeyCode.S, KeyCode.DownArrow, KeyCode.W, KeyCode.UpArrow), 0);
        Vector3 position = hero.position + Vector3.ClampMagnitude(move,1) * (12 * dt);
        hero.position = new Vector3(Mathf.Clamp(position.x,-edge+1,edge-1), Mathf.Clamp(position.y,-9,9),0);
        hero.gameObject.SetActive(elapsed >= safeUntil || Mathf.FloorToInt(elapsed*16)%2 == 0);

        if (Input.GetKeyDown(KeyCode.LeftShift) && charge == 5) {
            charge = 0;
            pulseUntil = elapsed + 4;
            effects.PlayOneShot(overdriveActive, 0.8f);
        }
        if (Input.GetKey(KeyCode.Space) && elapsed >= fireTime) {
            bool overdrive = elapsed < pulseUntil;
            fireTime = elapsed + (overdrive ? .085f : .19f);
            effects.PlayOneShot(playerGun, 0.35f);
            Fire(hero.position + Vector3.up, Vector3.up * 24, false);
            if (overdrive) {
                Fire(hero.position, new Vector3(-5,23), false);
                Fire(hero.position, new Vector3(5,23), false);
            }
        }
        if (elapsed >= spawnTime) {
            Spawn();
            spawnTime = elapsed + Mathf.Max(.55f, 1.5f-elapsed*.009f);
        }
        MoveShips(dt);
        MoveShots(dt);
    }

    void Spawn() {
        int type = spawned++ % enemies.Length;
        float lane = Random.Range(-edge*.55f,edge*.55f);
        Transform body = Instantiate(enemies[type], new Vector3(lane,11,0), Quaternion.identity).transform;
        body.name = "Enemy " + type;
        SizeModel(body, type == 4 ? 2.6f : 1.9f);
        ships.Add(new Ship { body=body, type=type, lane=lane, health=type < 2 ? 1 : type == 4 ? 4 : 2, nextShot=1.5f });
    }

    void MoveShips(float dt) {
        for (int i=ships.Count-1; i>=0; i--) {
            Ship ship = ships[i];
            ship.age += dt;
            float t = ship.age;
            Vector3 p = ship.body.position;
            switch (ship.type) {
                case 0: p.y -= 4.7f*dt; break;
                case 1: p.x = ship.lane + 2.5f*Mathf.Sin(2.8f*t); p.y -= 3.8f*dt; break;
                case 2: p.x = Mathf.Lerp(p.x, hero.position.x, dt*.8f); p.y -= 5.6f*dt; break;
                case 3: p.x = ship.lane + 3*Mathf.Sin(4*t); p.y -= (3.3f+1.7f*Mathf.Cos(4*t))*dt; break;
                case 4: p.x = ship.lane + 3*Mathf.Sin(1.2f*t); p.y -= 2.9f*dt; break;
            }
            ship.body.position = p;
            if (p.y < -12) { RemoveShip(i); continue; }
            if ((p-hero.position).sqrMagnitude < 1.45f) { HitHero(); effects.PlayOneShot(enemyDeath, 0.6f); RemoveShip(i); continue; }
            if (ship.type >= 2 && t >= ship.nextShot && p.y > hero.position.y+1 && p.y < 9) {
                ship.nextShot = t + 1.6f;
                effects.PlayOneShot(enemyLaser, 0.4f);
                Fire(p, (hero.position-p).normalized*8, true);
            }
        }
    }

    void Fire(Vector3 origin, Vector3 velocity, bool hostile) {
        var bolt = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(bolt.GetComponent<Collider>());
        bolt.GetComponent<Renderer>().sharedMaterial = hostile ? red : blue;
        bolt.transform.position = origin;
        bolt.transform.up = velocity.normalized;
        bolt.transform.localScale = hostile ? new Vector3(.22f,.46f,.22f) : new Vector3(.16f,.62f,.16f);
        shots.Add(new Shot { body=bolt.transform, velocity=velocity, hostile=hostile });
    }

    void MoveShots(float dt) {
        for (int i=shots.Count-1; i>=0; i--) {
            Shot shot = shots[i];
            Vector3 p = shot.body.position += shot.velocity * dt;
            bool spent = Mathf.Abs(p.y)>12 || Mathf.Abs(p.x)>edge+2;
            if (shot.hostile && (p-hero.position).sqrMagnitude < .7f) {
                HitHero();
                spent = true;
            }
            if (!shot.hostile && !spent) {
                for (int j=ships.Count-1; j>=0; j--) {
                    Ship ship = ships[j];
                    if ((p-ship.body.position).sqrMagnitude > (ship.type == 4 ? 1.5f : .85f)) continue;
                    spent = true;
                    if (--ship.health == 0) {
                        score += 100 + ship.type*25;
                        effects.PlayOneShot(enemyDeath, 0.6f);
                        if (charge == 4) effects.PlayOneShot(overdriveReady, 0.8f);
                        charge = Mathf.Min(5,charge+1);
                        RemoveShip(j);
                    }
                    break;
                }
            }
            if (spent) { Destroy(shot.body.gameObject); shots.RemoveAt(i); }
        }
    }

    void RemoveShip(int index) {
        Destroy(ships[index].body.gameObject);
        ships.RemoveAt(index);
    }

    void HitHero() {
        if (shields == 0 || elapsed < safeUntil) return;
        shields--;
        safeUntil = elapsed + 1.1f;
        if (shields == 0) {
            hero.gameObject.SetActive(false);
            effects.Stop();
            effects.PlayOneShot(playerDeath, 1f);
            music.Stop();
        } else {
            effects.PlayOneShot(playerTakeDamage, 0.8f);
        }
    }

    void OnGUI() {
        GUI.matrix = Matrix4x4.Scale(new Vector3(Screen.width/960f,Screen.height/540f,1));
        GUI.Box(new Rect(14,14,932,53), "");
        var text = new GUIStyle(GUI.skin.label) { fontSize=18, alignment=TextAnchor.MiddleCenter };
        string power = elapsed < pulseUntil ? "ACTIVE " + (pulseUntil-elapsed).ToString("0.0") + "s" :
            charge == 5 ? "READY [LEFT SHIFT]" : charge + "/5";
        GUI.Label(new Rect(15,15,930,50),
            $"SCORE  {score:00000}       SHIELDS  {shields}/4       OVERDRIVE  {power}",text);
        if (started && shields > 0) return;
        GUI.Box(new Rect(200,160,560,220),"");
        var title = new GUIStyle(text) { fontSize=36, fontStyle=FontStyle.Bold };
        GUI.Label(new Rect(200,177,560,60), started ? "SHIP LOST" : "SPACE SHMUP",title);
        GUI.Label(new Rect(200,244,560,82), started ? $"Final score  {score}\nPress R to retry" :
            "WASD / arrows to steer   •   Hold SPACE to fire\nFive kills charge OVERDRIVE   •   LEFT SHIFT to activate",text);
        if (!started) GUI.Label(new Rect(200,324,560,38),"Press ENTER to launch",text);
    }
}



