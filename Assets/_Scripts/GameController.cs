﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MessagePack;
using UnityEngine;

public class GameController : MonoBehaviour
{
    private Camera dedicatedCapture;
    private Vector3 gazeToWorld;
    private float CENTER_X_L;
    private float CENTER_Y_C;
    private float CENTER_X_R;
    private float CENTER_Y_T;
    public GridController gridController;
    public GameObject wall;
    public List<TargetCirle> targets;
    private float wall_width = 1;
    private float wall_height = 1;
    private static System.Random rand = new System.Random();
    private int last_index;
    private int target_index;
    private float timeLeft;
    public TargetCirle last_target = null;
    private RaycastHit looking_at_circle;
    private RaycastHit looking_at_circle_before;
    private float target_timer;
    private bool calib_end = false;
    private bool only_one = true;
    public bool is_started = false;
    private Color success_color = new Color(0.07f, 0.8f, 0.07f, 1);
    public float choosenTime;
    public float travel_time = -1.0f;
    public string choosenMode = "";

    # region log value
    private LoggerBehavior logger;
    private float confidence;
    private Vector3 norm_pos;
    #endregion

    private bool oui = true;


    void Start()
    {
        // Set the timers's default time
        ResetTargetTimer();
        ResetTimer();


        dedicatedCapture = Camera.main;

        // Create const value for x, y min and max
        CENTER_X_L = wall_width / 3;
        CENTER_Y_C = wall_height / 3;
        CENTER_X_R = wall_width - wall_width / 3;
        CENTER_Y_T = wall_height - wall_height / 3;

        // Create the targets
        targets = new List<TargetCirle>();
        targets.Add(new TargetCirle(0, CENTER_X_L, 0, CENTER_Y_C));
        targets.Add(new TargetCirle(CENTER_X_L, CENTER_X_R, 0, CENTER_Y_C));
        targets.Add(new TargetCirle(CENTER_X_R, wall_width, 0, CENTER_Y_C));
        targets.Add(new TargetCirle(0, CENTER_X_L, CENTER_Y_C, CENTER_Y_T));
        targets.Add(new TargetCirle(CENTER_X_L, CENTER_X_R, CENTER_Y_C, CENTER_Y_T));
        targets.Add(new TargetCirle(CENTER_X_R, wall_width, CENTER_Y_C, CENTER_Y_T));
        targets.Add(new TargetCirle(0, CENTER_X_L, CENTER_Y_T, wall_height));
        targets.Add(new TargetCirle(CENTER_X_L, CENTER_X_R, CENTER_Y_T, wall_height));
        targets.Add(new TargetCirle(CENTER_X_R, wall_width, CENTER_Y_T, wall_height));

        logger = GetComponent<LoggerBehavior>();
    }

    void OnEnable()
    {
        if (PupilTools.IsConnected)
        {
            PupilTools.SubscribeTo("gaze");
            PupilTools.SubscribeTo("pupil.");

            PupilTools.OnReceiveData += CustomReceiveData;
        }
    }

    void CustomReceiveData(string topic, Dictionary<string, object> dictionary, byte[] thirdFrame = null)
    {
        if (topic.StartsWith("pupil"))
        {
            foreach (var item in dictionary)
            {
                switch (item.Key)
                {
                    case "confidence":
                        confidence = PupilTools.FloatFromDictionary(dictionary, item.Key);
                        break;
                    case "norm_pos": // Origin 0,0 at the bottom left and 1,1 at the top right.
                        norm_pos = PupilTools.VectorFromDictionary(dictionary, item.Key);
                        break;
                    case "ellipse":
                        var dictionaryForKey = PupilTools.DictionaryFromDictionary(dictionary, item.Key);
                        foreach (var pupilEllipse in dictionaryForKey)
                        {
                            switch (pupilEllipse.Key.ToString())
                            {
                                case "angle":
                                    var angle = (float)(double)pupilEllipse.Value;
                                    // Do stuff
                                    break;
                                case "center":
                                    //print("Center : " + PupilTools.ObjectToVector(pupilEllipse.Value));
                                    break;
                                case "axes":
                                    //print("Axes : " + PupilTools.ObjectToVector(pupilEllipse.Value));
                                    // Do stuff
                                    break;
                                default:
                                    break;
                            }
                        }
                        // Do stuff
                        break;
                    default:
                        break;
                }
            }
        }
        if (topic.StartsWith("gaze") && oui)
        {
            foreach (var item in dictionary)
            {
                switch (item.Key)
                {
                    case "2D":
                        var dictionaryForKey = PupilTools.DictionaryFromDictionary(dictionary, item.Key);
                        foreach (var twoDEllipse in dictionaryForKey)
                        {
                            print(twoDEllipse.Key + " : " + twoDEllipse.Value);
                        }
                        break;
                    case "gaze.2d.0.":
                    case "gaze.2d.1.":
                        print(item.Value);
                        break;
                    default:
                        break;
                }
                print(item.Key + " : " + item.Value);

            }
            oui = false;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (is_started)
        {
            if (choosenMode == "approx")
                StartApproxMode();
            else if (choosenMode == "shrink")
                StartShrinkMode();
            else print("No mode is selected. Please verify you have selected a mode");
        }
    }

    private void StartApproxMode()
    {

        // Check if calibration is ended, delete current target, create each target in centered position
        if (calib_end && only_one)
        {
            print("Calibration test end.");
            targets.ForEach(t =>
            {
                if (t.circle_created)
                {
                    t.DestroyTarget();
                }
                t.CreateTarget(wall, true);
            });
            only_one = false;
        }
        else if (only_one)
        {
            // Get the current object looked at
            looking_at_circle = gridController.GetCurrentCollider();
            timeLeft -= Time.deltaTime;
            if (timeLeft < 0)
            {
                ResetTimer();
                // Destroy last target
                if (last_target != null)
                {
                    last_target.DestroyTarget();
                    travel_time = -1.0f;
                }
                // Get a random target and spawn it
                TargetCirle trgt = selectItem();
                if (trgt != null)
                {
                    trgt.CreateTarget(wall, false);
                    //print(trgt.circle.transform.localScale.ToString("F5"));
                    last_target = trgt;
                }
            }
            // Process the target looked at 
            if (looking_at_circle.collider)
            {
                if (looking_at_circle.collider.name == "Cylinder")
                {
                    if (looking_at_circle_before.collider)
                    {
                        if (System.Object.ReferenceEquals(looking_at_circle.collider, looking_at_circle_before.collider))
                        {
                            if (travel_time < 0)
                            {
                                travel_time = timeLeft;
                                LogData();
                            }
                            // If the target looked at is the same as before, start time
                            target_timer -= Time.deltaTime;
                            if (target_timer < 10)
                            {
                                looking_at_circle.collider.GetComponent<Renderer>().material.color = success_color;
                            }
                        }
                        else
                        {
                            looking_at_circle.collider.GetComponent<Renderer>().material.color = Color.white;
                            ResetTargetTimer();
                        }
                    }
                    looking_at_circle_before = looking_at_circle;
                }
                else
                {
                    ResetTargetTimer();
                }
            }
            // If the target has been fixed
            if (target_timer < 0)
            {
                print("The target was looked for 300ms");
                timeLeft = -1.0f;
                last_target.was_looked = true;
                ResetTargetTimer();
            }
        }
    }
    private void StartShrinkMode()
    {
        if (calib_end && only_one)
        {
            print("Calibration test end.");
            targets.ForEach(t =>
            {
                if (t.circle_created)
                {
                    t.DestroyTarget();
                }
                t.CreateTarget(wall, true);
            });
            only_one = false;
        }
        else if (only_one)
        {
            // Select a random target
            TargetCirle trgt = selectItem();
            // If no more time left and the target has not been looked.
            if (timeLeft < 0)
            {
                if (last_target != null)
                {
                    last_target.DestroyTarget();
                }
                // If the target is not created, create it
                if (!trgt.circle_created)
                {
                    trgt.CreateTarget(wall, true);
                    travel_time = -1.0f;
                    target_timer = 0.0f;
                }
                last_target = trgt;
                ResetTimer();
            }
            target_timer += Time.deltaTime;

            // Get the current object looked at by the user
            looking_at_circle = gridController.GetCurrentCollider();

            // If the user is looking the target, reduce its scale 
            if (looking_at_circle.collider)
            {
                if (looking_at_circle.collider.name == "Cylinder")
                {
                    if (travel_time < 0)
                    {
                        travel_time = target_timer - 0.3f;
                        LogData();
                        print("First time entry : " + travel_time);
                    }
                    last_target.was_looked = true;
                    last_target.ReduceScale();
                    looking_at_circle_before = looking_at_circle;
                }
                else
                {
                    timeLeft -= Time.deltaTime;
                }
                if (last_target != null)
                {
                    if (last_target.was_looked && looking_at_circle.collider.name != "Cylinder")
                    {
                        last_target.calibration_max = true;
                    }
                }
            }
        }
    }
    private TargetCirle selectItem()
    {
        // If all targets are minimum size, calibration is endend
        if (targets.All(trg => trg.calibration_max))
        {
            calib_end = true;
            return null;
        }
        // Get a random target in the list
        // exclude the previous target and target with calibration ended
        TargetCirle target;
        if (targets.Where(t => !t.calibration_max).ToList().Count <= 1)
        {
            print("Only one remaining");
            target = targets.Find(t => !t.calibration_max);
        }
        else
        {
            List<TargetCirle> lst_trgt = targets.Where(t => !t.calibration_max).ToList();
            do
            {
                target_index = rand.Next(lst_trgt.Count);
            } while (last_index == target_index);
            last_index = target_index;
            target = lst_trgt[target_index];
        }

        return target;
    }
    private void ResetTargetTimer()
    {
        target_timer = 0.3f;
    }
    private void ResetTimer()
    {
        timeLeft = choosenTime;
    }

    private void LogData()
    {
        if (PupilData._2D.GazePosition != Vector2.zero)
        {
            gazeToWorld = dedicatedCapture.ViewportToWorldPoint(new Vector3(PupilData._2D.GazePosition.x, PupilData._2D.GazePosition.y, Camera.main.nearClipPlane));
        }
        logger.AddObjToLog(new
        {
            a = DateTime.Now,
            j = PupilData._2D.GazePosition != Vector2.zero ? PupilData._2D.GazePosition.x : double.NaN,
            k = PupilData._2D.GazePosition != Vector2.zero ? PupilData._2D.GazePosition.y : double.NaN,
            l = PupilData._2D.GazePosition != Vector2.zero ? gazeToWorld.x : float.NaN,
            m = PupilData._2D.GazePosition != Vector2.zero ? gazeToWorld.y : float.NaN,
            n = confidence, // confidence value calculated after calibration 
            o = travel_time,
            p = last_target != null ? last_target.circle.transform.localPosition.x : double.NaN,
            q = last_target != null ? last_target.circle.transform.localPosition.y : double.NaN
        });
    }
}
