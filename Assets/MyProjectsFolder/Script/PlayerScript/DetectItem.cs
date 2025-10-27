using UnityEngine;
using static UnityEditor.Progress;
using UnityEngine.InputSystem;
public class DetectItem : MonoBehaviour
{
    public float raycastDistance = 5f; // Raycast mesafesi
    private Outline currentOutline;

    public Transform objectHolder;

    public GameObject detectedObject;
    public GameObject objectInHand;
    void Update()
    {

        CheckForItem();
    }

    void CheckForItem()
    {
        Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
        RaycastHit hit;

        // Debug ýþýný sahnede görmek için
        Debug.DrawRay(ray.origin, ray.direction * raycastDistance, Color.red);

        if (Physics.Raycast(ray, out hit, raycastDistance))
        {
            Item item = hit.collider.GetComponent<Item>();

            if (item != null)
            {
                Outline outline = item.GetComponent<Outline>();

                if (outline != null)
                {
                    if (currentOutline != outline)
                    {
                        if (currentOutline != null)
                            currentOutline.enabled = false;

                        outline.enabled = true;
                        detectedObject = item.gameObject;
                        currentOutline = outline;
                    }
                    return;
                }
            }

           
            }

        // Eðer ýþýn bir item bulamazsa, önceki outline'ý kapat
        if (currentOutline != null)
        {
            currentOutline.enabled = false;
            currentOutline = null;
            detectedObject = null;
        }
    }

    public void CheckCashRegister()
    {
        Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
        RaycastHit hit;

        // Debug ýþýný sahnede görmek için
        Debug.DrawRay(ray.origin, ray.direction * raycastDistance, Color.red);

        if (Physics.Raycast(ray, out hit, raycastDistance))
        {
            CashRegister register = hit.collider.GetComponent<CashRegister>();

            if (register != null)
            {
                Outline outline = register.GetComponent<Outline>();

                if (outline != null)
                {
                    if (currentOutline != outline)
                    {
                        if (currentOutline != null)
                            currentOutline.enabled = false;

                        outline.enabled = true;
                        currentOutline = outline;
                    }
                    return;
                }
            }


        }

        // Eðer ýþýn bir item bulamazsa, önceki outline'ý kapat
        if (currentOutline != null)
        {
            currentOutline.enabled = false;
            currentOutline = null;
        }
    }
    public void TakeObject()
    {
        if (detectedObject != null)
        {
            objectInHand = detectedObject;
            objectInHand.transform.parent = objectHolder;
            objectInHand.transform.localPosition = objectHolder.localPosition;
            objectInHand.GetComponent<Collider>().enabled = false;
            objectInHand.GetComponent<Rigidbody>().isKinematic = true;
        }
    }
    public void OnDrop()
    {
        if(objectInHand != null)
        {
            objectInHand.transform.parent = null;
            objectInHand.GetComponent<Collider>().enabled = true;
            objectInHand.GetComponent<Rigidbody>().isKinematic = false;
        }
    }
    
}
