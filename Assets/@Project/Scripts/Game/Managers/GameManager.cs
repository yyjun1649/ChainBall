using Library;


public class GameManager : SingletonBehaviour<GameManager>
{
    private GameHandler[] _handlers;
    
    protected override void Awake()
    {
        _handlers = GetComponentsInChildren<GameHandler>();
        
        base.Awake();
    }

    public void Initialize()
    {
        foreach (var gameHandler in _handlers)
        {
            gameHandler.Initialize();
        }
        
        foreach (var gameHandler in _handlers)
        {
            gameHandler.LateInitialize();
        }
    }

    public void StartBattle()
    {
        foreach (var handler in _handlers)
        {
            handler.StartGame();
        }
    }

    public void Clear()
    {
        foreach (var gameHandler in _handlers)
        {
            gameHandler.Clear();
        }
    }

    public void FailGame()
    {
    }
    

    public void Reset()
    {
        
    }
}
