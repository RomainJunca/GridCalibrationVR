﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor.VersionControl;

public class TargetCirle
{
    private Vector3 previous_scale;
    private float speed = 1.2f;
    public Vector3 scale_to_reach = new Vector3(0.25f, 0.01f, 0.3f);
    private Vector3 position_to_reach;
    public List<Vector3> previous_scales = new List<Vector3>();
    private List<float> scales_factor = new List<float>();
    public Material[] target_material = new Material[2];
    public Material[] target_center_material = new Material[2];
    public float highlightWidth = 0;
    private Texture2D target_texture;
    private float default_x_max;
    private float default_y_max;
    private float default_x_min;
    private float default_y_min;
    private float x_min;
    private float y_min;
    private float x_max;
    private float y_max;
    public GameObject circle;
    public GameObject dot;
    public bool was_looked;
    private List<bool> l_looked = new List<bool>();
    public bool calibration_max = false;
    public int calib_failed;
    public bool circle_created;
    private bool missed_four_times_before;

    private float target_shrinking = 0.999f; //The original value was 0.95
    private float timer = 0;
    private bool startMovingCircle = true;
    private bool startReGrowthCircle = true;
    private bool isSizeOk = false;
    private bool isPositionOk = false;
    private bool isCirleGoodSize = false;
    public float SPEED_OF_CIRCLE;

    private Color newOutlineColor;
    public float diff;
    public float dispersion = 0;
    public float distance = 0;
    private float timeToGrow = 0;
    private float distToGrow = 0;

    public TargetCirle(float x_min, float x_max, float y_min, float y_max)
    {
        default_x_min = x_min;
        default_x_max = x_max;
        default_y_min = y_min;
        default_y_max = y_max;
        previous_scale = scale_to_reach;
        calib_failed = 0;
        ResetScale();
    }
    internal void CreateTarget(GameObject wall, bool centered, int mode, bool endGame, Vector3 scale = default(Vector3))
    {
        l_looked.Add(was_looked);
        // Create the Circle 
        circle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        circle_created = true;
        // Set the Circle as child of the wall
        circle.transform.parent = wall.transform;
        circle.transform.localRotation = Quaternion.Euler(90, 0, 0);
        circle.layer = 12; // layer Circle
        if (!endGame)
        {
            if (scale == Vector3.zero)
            {
                scale = scale_to_reach;
            }

            if (mode == 1)
            {
                circle.transform.localScale = new Vector3(1.048261f, circle.transform.localScale.y, 1.262631f);
            }
            else
            {
                circle.transform.localScale = scale;
            }
        }
        else
        {
            circle.transform.localScale = previous_scale;
        }


        // Add red dot at the center of the target
        dot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        dot.gameObject.name = "Dot";
        dot.transform.parent = circle.transform;
        dot.transform.localRotation = Quaternion.Euler(0, 0, 0);
        dot.transform.localScale = new Vector3(0.09f, 1f, 0.09f);
        dot.transform.localPosition = new Vector3(0f, -1.1f, 0f);

        //Add material to the center of the target
        target_center_material[0] = (Material)Resources.Load("Red");
        target_center_material[1] = (Material)Resources.Load("Outline_Red");
        target_center_material[1].SetFloat("_OutlineWidth", 0);
        dot.GetComponent<Renderer>().materials = target_center_material;
        dot.GetComponent<Renderer>().material.color = new Color(1f, 0f, 0f, 1);

        //Add material to the target
        target_material[0] = (Material)Resources.Load("Target");
        target_material[1] = (Material)Resources.Load("Outline");
        target_material[1].SetFloat("_OutlineWidth", 0);
        target_texture = Resources.Load("Square") as Texture2D;
        circle.AddComponent<MeshCollider>();
        circle.GetComponent<CapsuleCollider>().enabled = false;
        circle.GetComponent<Renderer>().materials = target_material;  
        circle.GetComponent<Renderer>().material.mainTexture = target_texture;

        CalculateScale();
        CalculateOffset();
        if (centered)
        {
            // Place the circle at the center of the cell, for the end process
            circle.transform.localPosition = new Vector3((x_max + x_min) / 2, (y_max + y_min) / 2, -0.5f);
        }
        else
        {
            circle.transform.localPosition = new Vector3(Random.Range(x_min, x_max), Random.Range(y_min, y_max), -0.5f);
        }

        previous_scale = circle.transform.localScale;
        previous_scales.Add(previous_scale);
        was_looked = false;

        if (calib_failed > 2)
        {
            calibration_max = true;
        }
    }

    private void CalculateOffset()
    {
        x_min = default_x_min + circle.transform.localScale.x / 2;
        x_max = default_x_max - circle.transform.localScale.x / 2;
        y_min = default_y_min + circle.transform.localScale.z / 2;
        y_max = default_y_max - circle.transform.localScale.z / 2;
    }
    private void CalculateScale()
    {
        float last_scale_factor = GetLastScale();
        // If the target was looked at least once
        if (l_looked.Find(l => l))
        {
            //if the target is looked 
            if (was_looked)
            {
                scales_factor.Add(last_scale_factor);
                circle.transform.localScale /= last_scale_factor + 1;
                missed_four_times_before = false;
            }
            // if the targe has been missed 4 times before return to the last-last fixed target
            else if (missed_four_times_before)
            {
                circle.transform.localScale = previous_scales.Reverse<Vector3>().ToList()[l_looked.Reverse<bool>().ToList().IndexOf(l_looked.Reverse<bool>().ToList().Where(l => l).ToList()[1])];
                missed_four_times_before = false;
            }
            // If the target is missed reduce the scale factor
            else
            {
                scales_factor.Add(last_scale_factor / 1.1f);
                // get the last good scale by reversing the list and taking first true looked value index of the reversed looked list
                Vector3 last_good_scale = previous_scales.Reverse<Vector3>().ToList()[l_looked.Reverse<bool>().ToList().FindIndex(l => l)];
                circle.transform.localScale += (last_good_scale - previous_scale) / 2;
            }

            // If the last four times the target was NOT looked
            if (l_looked.Reverse<bool>().Take(5).ToList().Where(l => !l).ToList().Count == 5)
            {
                calibration_max = true;
            }
        }
    }

    private void ResetScale()
    {
        scales_factor.Add(0.5f);
    }

    internal float GetLastScale()
    {
        return scales_factor[scales_factor.Count - 1];
    }

    internal void DestroyTarget()
    {
        circle_created = false;
        Object.Destroy(circle);
    }

    internal void ReduceScale() 
    {

            circle.transform.localScale *= target_shrinking;
            dot.transform.localScale = new Vector3(0.0175f / circle.transform.localScale.x, 1.0f, 0.021f / circle.transform.localScale.z);
            previous_scale = circle.transform.localScale;
            previous_scales.Add(previous_scale);
    }

    internal void reduceSpeed(float dis, float coef, int mode) //Reduce the speed depending of the mode and a given coefficient
    {

        timer += Time.deltaTime;
        if(dis == 0 && mode == 1 && timer < 0.55f)
        {
            diff = 0;
            target_shrinking -= 0.001f;
        }
        else
        {
            diff = coef * 2 - (coef * dis);
        }

        if (diff > 0 && diff < 1)
        {
            target_shrinking = 0.999f-diff;
        }
        else
        {
            target_shrinking = 0.99f;
        }
    }

    internal void outlinePulse(Material mat, float max, float step) //The pulsation is set with a max value of width and a step
    {

        if(highlightWidth < max)
        {
            highlightWidth += step;
        }
        else
        {
            highlightWidth = 0;
        }
        mat.SetFloat("_OutlineWidth", highlightWidth);
    }

    internal bool bigCircleMode() 
    {

        if (circle.transform.localScale.x * 0.98f > scale_to_reach.x) //While the decreasing local scale is bigger than the normal scale for a target
        {
            circle.transform.localScale *= 0.988f;
            isCirleGoodSize = false;
            return false;
        }
        else
        {
            if (!isCirleGoodSize) //If the local scale went under the normal scale for a target, the local scale become the normal scale
            {
                circle.transform.localScale = scale_to_reach;
                isCirleGoodSize = true;
            }
            return true;
        }

    }

    internal bool movingCircleMode(Vector3 savedScale, Vector3 savedPosition)
    {
        if (!isPositionOk)
        {
            if (startMovingCircle) //We save the local position of the newly created circle, and for now we give it the position of the old circle
            {
                position_to_reach = circle.transform.localPosition;
                circle.transform.localPosition = savedPosition;
                startMovingCircle = false;
                isPositionOk = false;
                distance = Vector3.Distance(circle.transform.localPosition, position_to_reach); //We calculate the distance between the old and new target
                timeToGrow = distance / SPEED_OF_CIRCLE;
            }
            else
            {

                if (circle.transform.localPosition != position_to_reach) //While the circle is not in the right position, we move it with a speed depending on how much time the circle will have to grow back to its original size
                {
                    float step = SPEED_OF_CIRCLE * Time.deltaTime;
                    circle.transform.localPosition = Vector3.MoveTowards(circle.transform.localPosition, position_to_reach, step);
                    isPositionOk = false;
                }
                else
                {

                    startMovingCircle = true;
                    isPositionOk = true;
                }
            }

        }

        if (!isSizeOk)
        {

            if (circle.transform.localScale.x == scale_to_reach.x)
            {
                isSizeOk = true;
            }
            else
            {

                if (startReGrowthCircle) //We save the current scale of the new circle and give it the last scale of the old circle
                {
                    circle.transform.localScale = savedScale;
                    startReGrowthCircle = false;
                    isSizeOk = false;
                    distToGrow = Vector3.Distance(circle.transform.localScale, scale_to_reach);
                }
                else
                {
                    float step = (distToGrow/timeToGrow) * Time.deltaTime; //the step value is calculated depending of the calculated time for the target to grow back to the normal size
                    if (circle.transform.localScale.x * step < scale_to_reach.x) //While the increasing local scale is smaller than the normal scale for a target
                    {
                        circle.transform.localScale = Vector3.MoveTowards(circle.transform.localScale, scale_to_reach, step);
                        isSizeOk = false;
                    }
                    else //If the local scale went bigger than the normal scale for a target, the local scale become the normal scale
                    {
                        circle.transform.localScale = scale_to_reach;
                        startReGrowthCircle = true;
                        isSizeOk = true;
                    }
                }
            }
        }

        if(isPositionOk && isSizeOk) //If the size and position of the target are the "normal" one
        {
            isPositionOk = false;
            isSizeOk = false;
            return true;
        }
        else
        {
            return false;
        }
        
    }
}
