using UnityEngine;

public class TriggerAnimController : MonoBehaviour
{
    [SerializeField] private Animator m_Animator;

    private void Awake()
    {
        if (m_Animator == null)
        {
            m_Animator = GetComponent<Animator>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (m_Animator != null)
            {
                m_Animator.SetBool("Trigger", true);
            }
        }
    }
}
