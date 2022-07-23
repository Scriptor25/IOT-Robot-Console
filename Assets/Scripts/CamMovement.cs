using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CamMovement : MonoBehaviour
{
    public Transform camPivot;

    Vector2 rot = new Vector2(25, 0);

    public bool keyControlled = true;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        float x = 0;
        float y = 0;

        float zoom = Input.GetAxisRaw("Mouse ScrollWheel");

        if (keyControlled)
        {
            x = Input.GetAxis("Vertical");
            y = -Input.GetAxis("Horizontal");
        }
        else
        {
            if (Input.GetMouseButton(1))
            {
                x = -Input.GetAxis("Mouse Y");
                y = Input.GetAxis("Mouse X");
            }
            
        }

        //x *= Time.deltaTime * 200f;
        //y *= Time.deltaTime * 200f;
        //zoom *= Time.deltaTime * 250f;

        rot.x += x;
        rot.y += y;

        rot.x = Mathf.Clamp(rot.x, -90.0f, 90.0f);

        camPivot.rotation = Quaternion.Euler(rot);

        transform.Translate(new Vector3(0, 0, zoom), camPivot);

        if (transform.localPosition.z > 0) transform.localPosition = new Vector3(0, 0, 0);
        if (transform.localPosition.z < -20) transform.localPosition = new Vector3(0, 0, -20);
    }
}
