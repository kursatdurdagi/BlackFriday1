using UnityEngine;
using UnityEngine.InputSystem;
public class TakingScript : MonoBehaviour
{
    Animator anim;

    void Start()
    {
        anim = GetComponent<Animator>();
    }


    void Update()
    {
        
    }

    public void OnTaking()
    {
        anim.SetTrigger("Taking");

    }

}
