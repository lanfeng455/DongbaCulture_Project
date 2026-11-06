// One state of a dynamic entity (State pattern). States are plain classes instead of
// MonoBehaviours so they stay cheap to create and are registered on an EntityStateMachine.
public interface IEntityState
{
    void Enter();

    void Exit();

    void Tick(float deltaTime);
}
