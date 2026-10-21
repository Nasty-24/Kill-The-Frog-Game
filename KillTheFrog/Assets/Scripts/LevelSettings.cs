using System;

// Difficulty values for one level. GameManager moves to the next level
// once the player's score reaches scoreToReach.
[Serializable]
public class LevelSettings
{
    public string name = "Level";
    public int scoreToReach;
    public float spawnInterval = 1.4f;   // seconds between frog spawns
    public float frogLifetime = 3f;      // seconds a frog stays before escaping
    public float hopForce = 2.5f;        // impulse applied on each hop
    public int maxFrogs = 3;             // frogs allowed on screen at once
}
