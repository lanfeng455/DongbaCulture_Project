using System.Collections.Generic;
using UnityEngine;

// Minimal State pattern holder for dynamic entities: register states once, then Switch.
// Static entities simply never add this component - that is the whole difference.
public class EntityStateMachine : MonoBehaviour
{
    private readonly List<IEntityState> m_states = new();
    private IEntityState m_current;

    public IEntityState Current => m_current;

    public void Register(IEntityState state)
    {
        if (state != null && !m_states.Contains(state))
        {
            m_states.Add(state);
        }
    }

    // Switching to the active state is a no-op, so callers never need to check first
    public void Switch(IEntityState nextState)
    {
        if (nextState == null || nextState == m_current || !m_states.Contains(nextState))
        {
            return;
        }

        m_current?.Exit();
        m_current = nextState;
        m_current.Enter();
    }

    private void Update()
    {
        m_current?.Tick(Time.deltaTime);
    }
}
