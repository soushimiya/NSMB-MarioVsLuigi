public class ScriptableManager : Singleton<ScriptableManager> {

    public PrefabList         prefabs;
    public Powerup[]          powerups;
    public PlayerColorSet[]   skins;
    public CharacterData[]    characters;
    public Team[]             teams;
    public LoopingMusicData[] alternatingStageMusic;

    public void Awake() => Set(this);
}
