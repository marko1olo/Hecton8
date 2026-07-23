using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]

public class CandiceAIPlayerController2D : MonoBehaviour
{

    //player movement
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] float speed = 4.0f;    
    public float animSpeedControl = 1f; //animation speed control
    [SerializeField] float jumpForce = 7.5f;
    #pragma warning disable CS0414
    [SerializeField] float rollForce = 6.0f;

    //facing
    private int direction = 1;
    private bool grounded = false;
    #pragma warning restore CS0414
    private bool rolling = false;

    // Start is called before the first frame update
    void Start()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }

    // Update is called once per frame
    void Update()
    {
        // -- Handle input and movement --
        float inputX = Input.GetAxis("Horizontal");

        // Swap direction of sprite depending on walk direction
        //if (inputX > 0)
        //{
        //    spriteRenderer.flipX = false;
        //    direction = 1;
        //}

        //else if (inputX < 0)
        //{
        //    spriteRenderer.flipX = true;
        //    direction = -1;
        //}

        // Move
        if (!rolling)
            rb.linearVelocity = new Vector2(inputX * speed, rb.linearVelocity.y);

        //Jump
        if (Input.GetKeyDown("space"))
        {
            //m_animator.SetTrigger("Jump");
            //audioManager.Play("GruntVoice02");
            //grounded = false;
            //m_animator.SetBool("Grounded", grounded);
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            //m_groundSensor.Disable(0.2f);
        }

    }
}
