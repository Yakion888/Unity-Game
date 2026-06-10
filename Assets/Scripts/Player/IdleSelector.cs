using UnityEngine;

public class IdleSelector : MonoBehaviour
{
    private Animator anim;
    private float idleTimer = 0f;
    public float idleTimeToSwitch = 5f;

    void Start()
    {
        anim = GetComponent<Animator>();
    }

    void Update()
    {
        if (anim == null) return;

        float currentSpeed = anim.GetFloat("Speed");
        
        if (Mathf.Abs(currentSpeed) < 0.1f)
        {
            idleTimer += Time.deltaTime;

            if (idleTimer >= idleTimeToSwitch)
            {
                int randomIdle = Random.Range(0, 2);
                anim.SetFloat("IdleIndex", randomIdle);
                idleTimer = idleTimer - idleTimeToSwitch;
            }
        }
        else
        {
            idleTimer = 0f;
            // 🎯 移动时确保回到 idle1
            anim.SetFloat("IdleIndex", 0f);
        }
    }
    
    // 🎯 新增：外部调用重置计时器
    public void ResetIdleTimer()
    {
        idleTimer = 0f;
        if (anim != null)
        {
            anim.SetFloat("IdleIndex", 0f);
        }
    }
}