using UnityEngine;
using System.Collections;

public class FishHunt {

    SwimmingCreature predator;
    SwimmingCreature prey;
    float startTime = 1;
    float currentTime = 1;
    Vector3 predatorStart;
    Vector3 preyStart;
    private bool started;

    public FishHunt(SwimmingCreature predator, SwimmingCreature prey, float time)
    {
        this.predator = predator;
        this.prey = prey;
        startTime = time;
        currentTime = time;
        predatorStart = predator.transform.position;
        preyStart = prey.transform.position;
        started = false;
    }

    public void update()
    {
        if (!started)
        {
            started = true;
            predatorStart = predator.transform.position;
        }
        currentTime -= Time.deltaTime;
        if (currentTime <= 0)
        {
            currentTime = 0;
            prey.dyingTimer = 0;
        }
        float ratio = currentTime / startTime;
        if (predator.level != 2)
        {
            predator.transform.position = new Vector3(prey.transform.position.x + (predatorStart.x - prey.transform.position.x) * ratio,
            prey.transform.position.y + (predatorStart.y - prey.transform.position.y) * ratio, predatorStart.z);
            predator.velocity = new Vector2(prey.transform.position.x - predatorStart.x * ratio, prey.transform.position.y - predatorStart.y * ratio);
            predator.acceleration = Vector2.zero;
        }
        else
        {
            prey.transform.position = new Vector3(predator.transform.position.x + (preyStart.x - predator.transform.position.x) * ratio,
            predator.transform.position.y + (preyStart.y - predator.transform.position.y) * ratio, preyStart.z);
            prey.velocity = new Vector2(predator.transform.position.x - preyStart.x * ratio, predator.transform.position.y - preyStart.y * ratio);
            prey.acceleration = Vector2.zero;
        }
    }

    public bool isDone()
    {
        return currentTime == 0;
    }
}
