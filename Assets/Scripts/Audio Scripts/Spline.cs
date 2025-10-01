using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spline : MonoBehaviour
{
    //Creates an array of spline points
    private Vector3[] splinePoint;
    // int variabele for the number of spline points
    private int splineCount;
    // A Debug to be able to see the spline
    public bool debug_drawSpline = true;
    
    // Start is called before the first frame update
    void Start()
    {
        //splineCount now becomes the value of how many children there are to this object (object where the script is attached).
        //We can add empy game objects to our current game object and this will automatically know how many there are.
        splineCount = transform.childCount;
        //Each of the arrays become a Vector3 to the number of splineCount
        splinePoint = new Vector3[splineCount];
        
        //As long as the number identified as i (index) starts at 0 as long as its total number is below the maximum number of splineCount you run this loop over and over.
        for (int i = 0; i < splineCount; i++)
        {
            //splinePoint[i] = 0, so its the first point is equals the transform of the child 0 and its position and it does so again and again.
            splinePoint[i] = transform.GetChild(i).position;
        }
    }

    // Update is called once per frame
    // In order to draw a line we need to use the update function since it draws the line per frame.
    private void Update()
    {
        // If splineCount is greater than 1 then we draw a line
        if (debug_drawSpline && splineCount > 1)
        {
            // Value of i equals 0 and i is greater than splineCount++
            for (int i = 1; i < splineCount; i++)
            {
                // Draws a line between splinePoint[i] and splinePoint[i+1].
                //Means that i drwas a line between vector3 of the first object to the next object and then loops. Ex. Draw from value 1-2, 2-3..... 
                Debug.DrawLine(splinePoint[i-1], splinePoint[i],Color.green);
            }
        }
    }

    public Vector3 WhereOnSpline(Vector3 pos)
    {
        int closestSplinePoint = GetClosestSplinePoint(pos);

        if (closestSplinePoint == 0)
        {
            return splineSegment(splinePoint[0], splinePoint[1], pos);
        }
        else if (closestSplinePoint == splineCount - 1)
        {
            return splineSegment(splinePoint[splineCount - 1], splinePoint[splineCount - 2], pos);
        }
        else
        {
            Vector3 leftSeg = splineSegment(splinePoint[closestSplinePoint - 1], splinePoint[closestSplinePoint], pos);
            Vector3 rightSeg = splineSegment(splinePoint[closestSplinePoint + 1], splinePoint[closestSplinePoint], pos);

            if ((pos - leftSeg).sqrMagnitude <= (pos - rightSeg).sqrMagnitude)
            {
                return leftSeg;
            }
            else
            {
                return rightSeg;
            }
        }
    }

    private int GetClosestSplinePoint(Vector3 pos)
    {
        int closestPoint = -1;
        float shortstDistance = 0.0f;

        for (int i = 0; i < splineCount; i++)
        {
            float sqrDistance = (splinePoint[i] - pos).sqrMagnitude;
            if (shortstDistance == 0.0f || sqrDistance < shortstDistance)
            {
                shortstDistance = sqrDistance;
                closestPoint = i;
            }
        }

        return closestPoint;
    }

    public Vector3 splineSegment(Vector3 v1, Vector3 v2, Vector3 pos)
    {
        Vector3 v1ToPos = pos - v1;
        Vector3 segDirection = (v2 - v1).normalized;

        float distanceFromV1 = Vector3.Dot(segDirection, v1ToPos);

        if (distanceFromV1 < 0.0f)
        {
            return v1;
        }
        else if (distanceFromV1 * distanceFromV1 > (v2 - v1).sqrMagnitude)
        {
            return v2;
        }
        else
        {
            Vector3 fromV1 = segDirection * distanceFromV1;
            return v1 + fromV1;
        }
    }
}
