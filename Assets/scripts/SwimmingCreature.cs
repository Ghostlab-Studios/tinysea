using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class SwimmingCreature : MonoBehaviour {

    //boundary
    public Rect bounds = new Rect(-20, -20, 20, 20);

    public float edgeRadius = 5f;
    public float edgePower = 1;

    public int level = 1;

    public int id = 0;

    public List<SwimmingCreature> creatureFlock;

    // these are unnecessary
    //radii for responding to other creatures
    /*public float alignRadius = 2;
    public float attractRadius = 2;
    public float avoidRadius = .5f;
    public float fleeRadius = 10;
    public float huntRadius = 10;*/

    public float alignPower = 1;
    public float attractPower = 1;
    public float avoidPower = 1;
    public float fleePower = 1;
    public float huntPower = 1;

    //save our squared distances to avoid square-root calculations when comparing distances
    private float alignSq = 4;
    private float attractSq = 4;
    private float avoidSq = .25f;
    private float fleeSq = 100;
    private float huntSq = 100;

    //maximum movement speed
    public float maxSpeed = 30;
    //maximum steering force
    public float maxForce = .03f;

    //movement
    public Vector2 velocity = new Vector2(0, 0);
    public Vector2 acceleration = new Vector2(0, 0);

    public bool rotates = true;

    //animator
    private Animator anim;

    //effects
    [HideInInspector]
    public bool isBusy = false;
    [HideInInspector]
    public bool isDying = false;
    [HideInInspector]
    public ParticleSystem deathParticles;
    [HideInInspector]
    public Lure lure;
    [HideInInspector]
    public List<FishHunt> huntingFish;
    [HideInInspector]
    public bool isSpawning = true;
    [HideInInspector]
    public FishDrop fishDrop;
    [HideInInspector]
    public ParticleSystem spawnParticles;
    enum DeathCause {Particle, Lure, Eaten};
    private DeathCause deathCause;
    enum SpawnCause {Reproduction, Bought};
    private SpawnCause spawnCause;
    public float deathTime = 1;
    [HideInInspector]
    public float dyingTimer = 0;
    public float spawnTime = 1;
    private float spawningTimer = 1;
    private Vector3 startingScale = Vector3.one;
    public float particleSize = 10;
    private float particleTimer = 0;
    private bool haveFishes = false;

	// Use this for initialization
	void Start () {
        anim = GetComponent<Animator>();
        huntingFish = new List<FishHunt>();
        startingScale = transform.localScale;
        //InvokeRepeating("Flock", 2.0f, 0.3f);
        StartCoroutine(ManageFrames(1.0f));
	}

    // Coroutine for flocking. Calling Flock in Update is too CPU intensive
    IEnumerator ManageFrames(float seconds)
    {
        while(true)
        {
            yield return new WaitForSeconds(seconds);
            Flock();
        }
    }
	
	// Update is called once per frame
	void Update () {
        //Debug.Log(haveFishes);
        if (isDying)
        {
            die();
        }
        else if (isSpawning)
        {
            spawn();
        }
        else if (huntingFish.Count > 0)
        {
            huntingFish[0].update();
            if (huntingFish[0].isDone())
            {
                huntingFish.Remove(huntingFish[0]);
            }
        }
        else
        {
            isBusy = false;
            transform.localScale = startingScale;
            //Flock(creatureFlock);
            //Flock();
        }

        //Debug.Log("v" + velocity + " a" + acceleration);

        velocity += acceleration;
        //cap velocity
        if (velocity.magnitude > maxSpeed )
        {
            velocity = velocity.normalized * maxSpeed;
        }

        //scale relative to framerate
        Vector3 frameVel = velocity * Time.deltaTime;

        //animate speed
        anim.speed = velocity.magnitude / maxSpeed;

        //move
        transform.position = new Vector3(transform.position.x + frameVel.x,
            transform.position.y + frameVel.y, transform.position.z);

        // get rotation relative to UP
        float zRot = Mathf.Atan2(velocity.y, velocity.x) - Mathf.Atan2(-1, 0);
        //set rotation
        if (rotates && velocity != Vector2.zero)
        {
            transform.rotation = Quaternion.Euler(new Vector3(0, 0, zRot * Mathf.Rad2Deg));
        }
    }

    //updates the flock
    private void Flock()
    {
        if (creatureFlock == null)
        {
            return;
        }
        //acceleration = Vector2.zero;

        Vector2 avoidTotal = Vector2.zero;
        Vector2 attractTotal = Vector2.zero;
        Vector2 alignTotal = Vector2.zero;
        Vector2 fleeTotal = Vector2.zero;
        Vector2 huntTotal = Vector2.zero;
        int avoidCount = 0;
        int attractCount = 0;
        int alignCount = 0;
        int fleeCount = 0;
        int huntCount = 0;

        for (int i = 0; i < creatureFlock.Count; i++)
        {
            SwimmingCreature c = creatureFlock[i];
            //don't flock with ourself
            if (c.GetInstanceID() != GetInstanceID())
            {
                Vector3 ourPos = transform.position;
                Vector3 theirPos = c.transform.position;
                //calculate the squared distance
                //this is faster than the real distance because we don't need to do square-root calculations
                //float distSq = Mathf.Pow((ourPos.x - theirPos.x), 2) +
                    //Mathf.Pow((ourPos.y - theirPos.y), 2);
                float distSq = (ourPos - theirPos).sqrMagnitude;

                float powerRatio = Mathf.Max(1, 1 / distSq);

                //flock within level
                if (c.level == level)
                {
                    //avoid neutral creatures
                    Vector2 av = Avoid(c, distSq);
                    if (av != Vector2.zero)
                    {
                        avoidTotal += av * powerRatio;
                        avoidCount++;
                    }
                    //flock with same species
                    if (c.id == id)
                    {
                        Vector2 at = Attract(c, distSq);
                        if (at != Vector2.zero)
                        {
                            attractTotal += at * powerRatio;
                            attractCount++;
                        }
                        Vector2 al = Align(c, distSq);
                        if (al != Vector2.zero)
                        {
                            //Debug.Log("A_L_ " + al);
                            alignTotal += al * powerRatio;
                            alignCount++;
                        }
                    }
                }
                else if (c.level > level)
                {
                    Vector2 fl = Flee(c, distSq);
                    if (fl != Vector2.zero)
                    {
                        fleeTotal += fl * powerRatio;
                        fleeCount++;
                    }
                }
                else
                {
                    Vector2 hn = Hunt(c, distSq);
                    if (hn != Vector2.zero)
                    {
                        huntTotal += hn * powerRatio;
                        huntCount++;
                    }
                }
            }
        }

        if (avoidCount != 0)
            avoidTotal = avoidTotal / avoidCount;
        if (attractCount != 0)
            attractTotal = attractTotal / attractCount;
        if (alignCount != 0)
            alignTotal = alignTotal / alignCount;
        if (fleeCount != 0)
            fleeTotal = fleeTotal / fleeCount;
        if (huntCount != 0)
            huntTotal = huntTotal / huntCount;

        acceleration = 
            EdgeSteer() * edgePower+ 
            avoidTotal * avoidPower + 
            attractTotal * attractPower +
            alignTotal * alignPower + 
            fleeTotal * fleePower+ 
            huntTotal * huntPower;

        if (acceleration.magnitude > maxForce)
        {
            acceleration = acceleration.normalized * maxForce;
        }
    }

    private Vector2 Avoid(SwimmingCreature creature, float distSq)
    {
        if (distSq < avoidSq)
        {
            Vector3 ourPos = transform.position;
            Vector3 theirPos = creature.transform.position;
            //go away from the other creature
            Vector2 avoidVector = new Vector2(ourPos.x - theirPos.x, ourPos.y - theirPos.y);
            avoidVector.Normalize();
            //TODO: weight by distance
            return avoidVector;
        }
        else
            return Vector2.zero;
    }

    private Vector2 Attract(SwimmingCreature creature, float distSq)
    {
        if (distSq < attractSq)
        {
            Vector3 ourPos = transform.position;
            Vector3 theirPos = creature.transform.position;
            //go towards the other creature
            Vector2 attractVector = new Vector2(theirPos.x - ourPos.x, theirPos.y - ourPos.y);
            attractVector.Normalize();
            //TODO: weight by distance
            return attractVector;
        }
        else
            return Vector2.zero;
    }

    private Vector2 Align(SwimmingCreature creature, float distSq)
    {
        if (distSq < alignSq)
        {
            //align with the other creature
            //Debug.Log("V- " + creature.velocity);
            Vector2 alignVector = creature.velocity;
            alignVector.Normalize();
            //Debug.Log("VV- " + alignVector);
            //TODO: weight by distance
            return alignVector;
        }
        else
            return Vector2.zero;
    }

    private Vector2 Flee(SwimmingCreature creature, float distSq)
    {
        if (distSq < fleeSq)
        {
            Vector3 ourPos = transform.position;
            Vector3 theirPos = creature.transform.position;
            //go away from the other creature
            Vector2 avoidVector = new Vector2(ourPos.x - theirPos.x, ourPos.y - theirPos.y);
            avoidVector.Normalize();
            //TODO: weight by distance
            return avoidVector;
        }
        else
            return Vector2.zero;
    }

    private Vector2 Hunt(SwimmingCreature creature, float distSq)
    {
        if (distSq < huntSq)
        {
            Vector3 ourPos = transform.position;
            Vector3 theirPos = creature.transform.position;
            //go towards the other creature
            Vector2 attractVector = new Vector2(theirPos.x - ourPos.x, theirPos.y - ourPos.y);
            attractVector.Normalize();
            //TODO: weight by distance
            return attractVector;
        }
        else
            return Vector2.zero;
    }

    private Vector2 EdgeSteer()
    {
        float x = transform.position.x;
        float y = transform.position.y;
        Vector2 steer = Vector2.zero;

        //"analog" steering (CPU intense)
        /*
        if (x < bounds.xMin + edgeRadius)
            steer += new Vector2((edgeRadius - (bounds.xMin - x)) / -edgeRadius, 0);
        if (x > bounds.xMax - edgeRadius)
            steer += new Vector2((edgeRadius - (bounds.xMax - x)) / -edgeRadius, 0);
        if (y < bounds.yMin + edgeRadius)
            steer += new Vector2(0, (edgeRadius - (bounds.yMax - y)) / -edgeRadius);
        if (y > bounds.yMax - edgeRadius)
            steer += new Vector2(0, (edgeRadius - (bounds.yMax - y)) / -edgeRadius);*/
        //"digital" steering
        if (x < bounds.xMin + edgeRadius)
            steer += new Vector2(1, 0);
        if (x > bounds.xMax - edgeRadius)
            steer += new Vector2(-1, 0);
        if (y < bounds.yMin + edgeRadius)
            steer += new Vector2(0, 1);
        if (y > bounds.yMax - edgeRadius)
            steer += new Vector2(0, -1);

        //hard edge
        /*if (x < bounds.xMin)
            transform.position = new Vector3(bounds.xMin, transform.position.y, transform.position.z);
        else if (x > bounds.xMax)
            transform.position = new Vector3(bounds.xMax, transform.position.y, transform.position.z);
        if (y < bounds.yMin)
            transform.position = new Vector3(transform.position.x, bounds.yMin, transform.position.z);
        else if (y > bounds.yMax)
            transform.position = new Vector3(transform.position.x, bounds.yMax, transform.position.z);*/

        return steer;
    }

    private void spawn()
    {
        velocity += new Vector2(Random.value, Random.value);
        if (spawningTimer <= 0)
        {
            velocity = new Vector2(Random.value, Random.value) * maxSpeed;
            isSpawning = false;
            spawningTimer = 0;
        }
        else
        {
            //tick timer
            spawningTimer -= Time.deltaTime;
            switch (spawnCause)
            {
                case SpawnCause.Reproduction:
                    haveFishes = true;
                    //flock
                    //Flock();
                    //tick timer
                    spawningTimer -= Time.deltaTime;
                    //grow
                    transform.localScale = startingScale * (1 - spawningTimer) / spawnTime;
                    //emit partiles according to parameters
                    particleTimer += Time.deltaTime;
                    while (particleTimer > 1 / spawnParticles.emissionRate)
                    {
                        particleTimer -= 1 / spawnParticles.emissionRate;
                        spawnParticles.Emit(transform.position,
                            particleSize * spawnParticles.startSpeed * 2 *
                            new Vector3(Random.value - .5f, Random.value - .5f, 0),
                            particleSize * spawnParticles.startSize, spawnParticles.startLifetime, Color.white);
                    }
                    break;
                case SpawnCause.Bought:
                    //only do stuff if we're being fished
                    haveFishes = true;
                    transform.position = fishDrop.getPos(spawningTimer / spawnTime);
                    break;
            }
        }
    }

    public void StartReproducing(ParticleSystem particles, Vector3 spawnPos)
    {
        isBusy = true;
        transform.position = spawnPos;
        spawnParticles = particles;
        spawnTime = spawnTime * (.5f + Random.value);
        spawningTimer = spawnTime;
        spawnCause = SpawnCause.Reproduction;
        isSpawning = true;
        //haveFishes = true;
    }

    public void StartBuying()
    {
        isBusy = true;
        spawnTime = spawnTime * (.5f + Random.value);
        spawningTimer = spawnTime;
        //Random.seed = GetInstanceID();
        fishDrop = new FishDrop(new Vector3(
            Random.Range(bounds.xMin + edgeRadius, bounds.xMax - edgeRadius), 
            Random.Range(bounds.yMin, bounds.yMax), 
            transform.position.z), 
            FishDrop.sceneHeight * (2 + .2f * Random.value) );
        transform.position = fishDrop.getPos(0);
        spawnCause = SpawnCause.Bought;
        isSpawning = true;
    }

    private void die()
    {
        if (dyingTimer <= 0)
        {
            KillForever();
        }
        else
        {
            switch (deathCause)
            {
                case DeathCause.Particle:
                    //flock
                    //Flock();
                    //tick timer
                    dyingTimer -= Time.deltaTime;
                    //shrink and die
                    transform.localScale = startingScale * dyingTimer / deathTime;
                    //emit partiles according to parameters
                    particleTimer += Time.deltaTime;
                    while(particleTimer > 1/deathParticles.emissionRate) {
                        particleTimer -= 1/deathParticles.emissionRate;
                        deathParticles.Emit(transform.position,
                            particleSize * deathParticles.startSpeed * 2 * 
                            new Vector3(Random.value - .5f, Random.value - .5f, 0),
                            particleSize * deathParticles.startSize, deathParticles.startLifetime, Color.white);
                    }
                    break;
                case DeathCause.Lure:
                    
                    //only do stuff if we're being fished
                    if (lure.amITheTarget(this))
                    {
                        //tick timer
                        dyingTimer -= Time.deltaTime;
                        //move with the lure
                        if (getDeathRatio() < .5f)
                        {
                            transform.position = lure.transform.position;
                            transform.rotation = Quaternion.EulerAngles(0, 0, 180);
                        }
                    }
                    else
                    {
                        //if we're not being fished, just swim around like we don't even know we're doomed
                        //Flock();
                    }
                    break;
                case DeathCause.Eaten:
                    //dying timer is all handled by the FishHunt object, which is updated by the predator. 
                    //the prey doesn't need to do anything other than swim around
                    //Flock();
                    break;
            }
        }
    }

    public void startDying(ParticleSystem cause)
    {
        isBusy = true;
        deathParticles = cause;
        isDying = true;
        deathTime = deathTime * (.5f + Random.value);
        dyingTimer = deathTime;
        deathCause = DeathCause.Particle;
    }

    public void multDeathTime(float dTime)
    {
        deathTime = deathTime*dTime;
        dyingTimer = dyingTimer*dTime;
    }

    public void startFishing(Lure cause)
    {
        isBusy = true;
        lure = cause;
        isDying = true;
        deathTime = deathTime * (.5f + Random.value);
        dyingTimer = deathTime;
        deathCause = DeathCause.Lure;
        lure.gameObject.SetActive(true);
        lure.addTarget(this);
        lure.Reset();
    }

    public void startEating()
    {
        isBusy = true;
    }

    public void getEaten(FishHunt hunt)
    {
        isBusy = true;
        isDying = true;
        deathCause = DeathCause.Eaten;
        deathTime = 1;
        dyingTimer = 1;
    }

    private void KillForever()
    {
        creatureFlock.Remove(this);
        Destroy(gameObject);
    }

    public float getDeathRatio()
    {
        return dyingTimer / deathTime;
    }
}
