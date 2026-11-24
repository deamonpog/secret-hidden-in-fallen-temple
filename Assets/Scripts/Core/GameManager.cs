using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Owns the state of the run: playing, paused, completed or failed. It also holds the seed
/// and the stars. Gameplay input and time are switched off whenever the run is not Playing.
/// </summary>
[DefaultExecutionOrder(50)]
public class GameManager : MonoBehaviour
{
    public const int MaxStars = 3;

    [SerializeField] private Player player;

    // Optional. Builds the level at the start of each run. Empty in the hand-built test scene.
    [SerializeField] private WorldGenerator generator;

    // The seed chosen for the next run. It is static so it survives the scene reload that
    // starts a new run. Zero means no seed was chosen yet.
    private static int pendingSeed;

    private PlayerInputReader input;

    public GameState State { get; private set; } = GameState.Boot;
    public int Seed { get; private set; }
    public int Stars { get; private set; }

    private readonly List<Shrine> shrines = new List<Shrine>();

    // Every shrine in the level, active or not. Tracker reads this.
    public IReadOnlyList<Shrine> Shrines => shrines;

    // Raised after the state changed, with the new state.
    public event Action<GameState> StateChanged;

    // Raised after the star count changed, with the new count.
    public event Action<int> StarsChanged;

    private void Awake()
    {
        if (player == null)
        {
            Debug.LogError("GameManager needs the Player assigned.", this);
            enabled = false;
            return;
        }

        // Player requires PlayerInputReader on its own object, so it is always there.
        input = player.GetComponent<PlayerInputReader>();
        Time.timeScale = 1f;
    }

    // Player has run its Awake before this (execution order 50), so Health exists.
    private void OnEnable()
    {
        input.PausePressed += OnPausePressed;
        player.Health.Died += OnPlayerDied;
    }

    private void OnDisable()
    {
        if (input != null)
            input.PausePressed -= OnPausePressed;

        if (player != null && player.Health != null)
            player.Health.Died -= OnPlayerDied;

        Time.timeScale = 1f;
    }

    private void Start()
    {
        StartRun();
    }

    // Begins a run with the chosen seed, or a new random one.
    private void StartRun()
    {
        Seed = pendingSeed != 0 ? pendingSeed : NewSeed();
        pendingSeed = 0;
        Stars = 0;

        SetState(GameState.Generating);

        if (generator != null)
            generator.Generate(Seed);

        SetState(GameState.Playing);
    }

    // Pauses or resumes. Only a running or paused game can change this way.
    public void SetPaused(bool paused)
    {
        if (paused && State == GameState.Playing)
            SetState(GameState.Paused);
        else if (!paused && State == GameState.Paused)
            SetState(GameState.Playing);
    }

    // Ends the run as won. Does nothing unless the run is being played.
    public void Complete()
    {
        if (State == GameState.Playing)
            SetState(GameState.Completed);
    }

    // Ends the run as lost. Does nothing unless the run is being played.
    public void Fail()
    {
        if (State == GameState.Playing)
            SetState(GameState.Failed);
    }

    // Adds a star, up to MaxStars. Does nothing unless the run is being played.
    public void AddStar()
    {
        if (State != GameState.Playing || Stars >= MaxStars)
            return;

        Stars++;
        StarsChanged?.Invoke(Stars);
    }

    // Starts the next run with a new seed by reloading the scene.
    public void NewRun()
    {
        pendingSeed = NewSeed();
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // Adds a shrine to the list. Shrines call this when they are enabled.
    public void RegisterShrine(Shrine shrine)
    {
        if (!shrines.Contains(shrine))
            shrines.Add(shrine);
    }

    // Removes a shrine from the list. Shrines call this when they are disabled.
    public void UnregisterShrine(Shrine shrine)
    {
        shrines.Remove(shrine);
    }

    private void OnPausePressed()
    {
        SetPaused(State == GameState.Playing);
    }

    private void OnPlayerDied()
    {
        Fail();
    }

    // Switches input and time to match the state. Paused freezes time. Failed and Completed
    // keep it running so the death and cheer animations play.
    private void SetState(GameState state)
    {
        if (State == state)
            return;

        State = state;
        input.GameplayEnabled = state == GameState.Playing;
        Time.timeScale = state == GameState.Paused ? 0f : 1f;
        StateChanged?.Invoke(state);
    }

    private static int NewSeed()
    {
        return UnityEngine.Random.Range(1, int.MaxValue);
    }
}