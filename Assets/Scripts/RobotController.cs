using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Robotics.ROSTCPConnector;

using CompressedImageMsg = RosMessageTypes.Sensor.CompressedImageMsg;
using Float32Msg = RosMessageTypes.Std.Float32Msg;
using Float32MultiArrayMsg = RosMessageTypes.Std.Float32MultiArrayMsg;
using HeaderMsg = RosMessageTypes.Std.HeaderMsg;
using JoyMsg = RosMessageTypes.Sensor.JoyMsg;
using PoseMsg = RosMessageTypes.Geometry.PoseStampedMsg;
using StringMsg = RosMessageTypes.Std.StringMsg;
using TimeMsg = RosMessageTypes.BuiltinInterfaces.TimeMsg;
using LaserScanMsg = RosMessageTypes.Sensor.LaserScanMsg;
using ImuMsg = RosMessageTypes.Sensor.ImuMsg;

using UnityEngine.VFX;

public class RobotController : MonoBehaviour
{
    // Ros Topics
    //public string nameSpace = "/wgg";
    public string calibrateRosTopic = "/calibrate";
    public string clockRosTopic = "/clock";
    public string currentRosTopic = "/current";
    public string imageRosTopic = "/camera/image_raw/compressed";
    public string imuRosTopic = "/imu";
    public string inputRosTopic = "/joy";
    public string lifeRosTopic = "/event_msg";
    public string poseRosTopic = "/pose";
    public string rpmRosTopic = "/rpm";
    public string scanRosTopic = "/scan";
    public string tofRosTopic = "/tof";
    public string voltageRosTopic = "/voltage";

    // Diff Image
    public float imageMotionThreshold = 0.2f;
    public int motionPixelThreshold = 1000;

    public RawImage cameraImage;
    public RawImage cameraDiffImage;

    Texture2D oldImage;
    Texture2D target;
    Texture2D diff;

    public string[] motionMsgs;

    bool firstImg = true;

    // Sensordata Views
    public Text voltageText;
    public Text rpmFrontLeftText;
    public Text rpmFrontRightText;
    public Text rpmRearLeftText;
    public Text rpmRearRightText;
    public Text tofFrontLeftText;
    public Text tofFrontRightText;
    public Text tofRearLeftText;
    public Text tofRearRightText;
    public Text poseText;
    public Text throttleText;
    public Text imuText;
    public Text currentText;

    // Time
    TimeMsg time;
    TimeMsg lastMotionTime;
    TimeMsg lastImgMotionTime;

    // ROS
    ROSConnection ros;

    // Scan
    List<Vector3> points = new List<Vector3>();
    List<Color> cols = new List<Color>();

    Texture2D texColor;
    Texture2D texPosScale;
    public VisualEffect scanVfx;
    uint resolution = 2048;

    public float particleSize = 0.1f;
    bool toUpdate = false;
    uint particleCount = 0;

    public Mesh meshToParticlize;
    public bool updateComplete = false;

    // Settings
    public InputField settingsCam;
    public InputField settingsJoy;
    public InputField settingsVolt;
    public InputField settingsTof;
    public InputField settingsRpm;
    public InputField settingsPose;
    public InputField settingsClock;
    public InputField settingsLife;
    public InputField settingsScan;
    public InputField settingsImu;
    public InputField settingsCalibrate;
    public InputField settingsCurrent;

    // Movement Input
    public float throttle = 0.3f;
    bool[] lightKeys = new bool[9];
    bool diffimg = false;

    private void Start()
    {
        //Texture2D.allowThreadedTextureCreation = false;

        ros = ROSConnection.GetOrCreateInstance();

        ros.RegisterPublisher<JoyMsg>(inputRosTopic);
        ros.RegisterPublisher<StringMsg>(lifeRosTopic);

        ros.Subscribe<CompressedImageMsg>(imageRosTopic, OnImageCallback);
        ros.Subscribe<Float32Msg>(voltageRosTopic, OnVoltageCallback);
        ros.Subscribe<Float32MultiArrayMsg>(tofRosTopic, OnToFCallback);
        ros.Subscribe<Float32MultiArrayMsg>(rpmRosTopic, OnRPMCallback);
        ros.Subscribe<PoseMsg>(poseRosTopic, OnPoseCallback);
        ros.Subscribe<HeaderMsg>(clockRosTopic, OnClockCallback);
        ros.Subscribe<LaserScanMsg>(scanRosTopic, OnScanCallback);
        ros.Subscribe<ImuMsg>(imuRosTopic, OnImuCallback);
        ros.Subscribe<Float32Msg>(currentRosTopic, OnCurrentCallback);

        ros.SendServiceMessage<RosMessageTypes.Std.EmptyResponse>(calibrateRosTopic, new RosMessageTypes.Std.EmptyRequest());

        if (motionMsgs == null || motionMsgs.Length == 0)
        {
            motionMsgs = new string[1];
            motionMsgs[0] = "WGG's IOT has detected strong movement patterns!";
        }

        lastMotionTime = new TimeMsg(0, 0);
        lastImgMotionTime = new TimeMsg(0, 0);
        time = new TimeMsg(0, 0);
    }

    private void Update()
    {
        // Check if there are any joysticks available
        if(Input.GetJoystickNames() != null && Input.GetJoystickNames().Length != 0)
        {
            // Joystick axis input
            throttle = 1.0f - ((Input.GetAxisRaw("Throttle") + 1) * 0.5f);
        }
        
        // Key input
        throttle += Input.GetAxisRaw("KeyThrottle") / 100.0f;

        // Clamp throttle value
        throttle = Mathf.Clamp(throttle, 0.05f, 1.0f);
        throttleText.text = (int)(throttle * 100) + "%";

        // Toggle diff image
        if (Input.GetKeyDown(KeyCode.Q)) diffimg = !diffimg;

        // Toggle lights
        if (Input.GetKeyDown(KeyCode.Alpha1)) lightKeys[0] = !lightKeys[0];
        if (Input.GetKeyDown(KeyCode.Alpha2)) lightKeys[1] = !lightKeys[1];
        if (Input.GetKeyDown(KeyCode.Alpha3)) lightKeys[2] = !lightKeys[2];
        if (Input.GetKeyDown(KeyCode.Alpha4)) lightKeys[3] = !lightKeys[3];
        if (Input.GetKeyDown(KeyCode.Alpha5)) lightKeys[4] = !lightKeys[4];
        if (Input.GetKeyDown(KeyCode.Alpha6)) lightKeys[5] = !lightKeys[5];
        if (Input.GetKeyDown(KeyCode.Alpha7)) lightKeys[6] = !lightKeys[6];
        if (Input.GetKeyDown(KeyCode.Alpha8)) lightKeys[7] = !lightKeys[7];
        if (Input.GetKeyDown(KeyCode.Alpha9)) lightKeys[8] = !lightKeys[8];

        // Get movement input
        float x = 0.5f * Input.GetAxisRaw("Horizontal");
        float y = Input.GetAxisRaw("Vertical");

        // Joystick msg for IOT
        JoyMsg joyMsg = new JoyMsg();

        joyMsg.header.frame_id = "joy";
        joyMsg.header.stamp = time;

        joyMsg.axes = new float[4];
        joyMsg.buttons = new int[12];

        joyMsg.axes[0] = 0;
        joyMsg.axes[1] = y;
        joyMsg.axes[2] = -x;
        joyMsg.axes[3] = throttle * 2 - 1.0f;

        joyMsg.buttons[0] = lightKeys[0] ? 1 : 0;
        joyMsg.buttons[1] = lightKeys[1] ? 1 : 0;
        joyMsg.buttons[2] = lightKeys[2] ? 1 : 0;
        joyMsg.buttons[3] = lightKeys[3] ? 1 : 0;
        joyMsg.buttons[4] = lightKeys[4] ? 1 : 0;
        joyMsg.buttons[5] = lightKeys[5] ? 1 : 0;
        joyMsg.buttons[6] = lightKeys[6] ? 1 : 0;
        joyMsg.buttons[7] = lightKeys[7] ? 1 : 0;
        joyMsg.buttons[8] = lightKeys[8] ? 1 : 0;
        joyMsg.buttons[9] = Input.GetKey(KeyCode.Alpha0) ? 1 : 0;
        joyMsg.buttons[10] = Input.GetKey(KeyCode.E) ? 1 : 0;
        joyMsg.buttons[11] = 0;

        ros.Publish(inputRosTopic, joyMsg);

        // Lidar pointcloud update
        if (updateComplete)
        {
            if (meshToParticlize != null)
            {
                points = new List<Vector3>();
                cols = new List<Color>();

                for (int i = 0; i < meshToParticlize.vertices.Length; i++)
                {
                    points.Add(meshToParticlize.vertices[i]);
                    cols.Add(Color.white);
                }
            }

            SetParticles(points.ToArray(), cols.ToArray());

            updateComplete = false;
        }

        if (toUpdate)
        {
            toUpdate = false;

            scanVfx.Reinit();
            scanVfx.SetUInt(Shader.PropertyToID("ParticleCount"), particleCount);
            scanVfx.SetTexture(Shader.PropertyToID("TexColor"), texColor);
            scanVfx.SetTexture(Shader.PropertyToID("TexPosScale"), texPosScale);
            scanVfx.SetUInt(Shader.PropertyToID("Resolution"), resolution);
        }
    }

    public void SetAllTopics()
    {
        SetAllTopics(settingsClock.text, settingsCam.text, settingsJoy.text, settingsLife.text, settingsPose.text, settingsRpm.text, settingsScan.text, settingsTof.text, settingsVolt.text, settingsImu.text, settingsCalibrate.text, settingsCurrent.text);
    }

    public void SetAllTopics(string clockTopic, string imageTopic, string inputTopic, string lifeTopic, string poseTopic, string rpmTopic, string scanTopic, string tofTopic, string voltageTopic, string imuTopic, string calibrateTopic, string currentTopic)
    {
        ros.Unsubscribe(clockRosTopic);
        ros.Unsubscribe(imageRosTopic);
        ros.Unsubscribe(inputRosTopic);
        ros.Unsubscribe(lifeRosTopic);
        ros.Unsubscribe(poseRosTopic);
        ros.Unsubscribe(rpmRosTopic);
        ros.Unsubscribe(scanRosTopic);
        ros.Unsubscribe(tofRosTopic);
        ros.Unsubscribe(voltageRosTopic);
        ros.Unsubscribe(imuRosTopic);
        ros.Unsubscribe(currentRosTopic);

        clockRosTopic = clockTopic == "" ? "/clock" : clockTopic;
        imageRosTopic = imageTopic == "" ? "/camera/image_raw/compressed" : imageTopic;
        inputRosTopic = inputTopic == "" ? "/joy" : inputTopic;
        lifeRosTopic = lifeTopic == "" ? "/life_detection" : lifeTopic;
        poseRosTopic = poseTopic == "" ? "/pose" : poseTopic;
        rpmRosTopic = rpmTopic == "" ? "/rpm" : rpmTopic;
        scanRosTopic = scanTopic == "" ? "/scan" : scanTopic;
        tofRosTopic = tofTopic == "" ? "/tof" : tofTopic;
        voltageRosTopic = voltageTopic == "" ? "/voltage" : voltageTopic;
        imuRosTopic = imuTopic == "" ? "/imu" : imuTopic;
        currentRosTopic = currentTopic == "" ? "/current" : currentTopic;
        calibrateRosTopic = calibrateTopic == "" ? "/calibrate" : calibrateTopic;

        ros = ROSConnection.GetOrCreateInstance();

        ros.RegisterPublisher<JoyMsg>(inputRosTopic);
        ros.RegisterPublisher<StringMsg>(lifeRosTopic);

        ros.Subscribe<CompressedImageMsg>(imageRosTopic, OnImageCallback);
        ros.Subscribe<Float32Msg>(voltageRosTopic, OnVoltageCallback);
        ros.Subscribe<Float32MultiArrayMsg>(tofRosTopic, OnToFCallback);
        ros.Subscribe<Float32MultiArrayMsg>(rpmRosTopic, OnRPMCallback);
        ros.Subscribe<PoseMsg>(poseRosTopic, OnPoseCallback);
        ros.Subscribe<HeaderMsg>(clockRosTopic, OnClockCallback);
        ros.Subscribe<LaserScanMsg>(scanRosTopic, OnScanCallback);
        ros.Subscribe<ImuMsg>(imuRosTopic, OnImuCallback);
        ros.Subscribe<Float32Msg>(currentRosTopic, OnCurrentCallback);

        ros.SendServiceMessage<RosMessageTypes.Std.EmptyResponse>(calibrateRosTopic, new RosMessageTypes.Std.EmptyRequest());
    }

    void OnCurrentCallback(Float32Msg msg)
    {
        currentText.text = msg.data + " V";
    }

    void OnClockCallback(HeaderMsg msg)
    {
        time = msg.stamp;
    }

    void OnImuCallback(ImuMsg msg)
    {
        string str = "Linear Acceleration: " + (int)msg.linear_acceleration.x + " " + msg.linear_acceleration.y + " " + msg.linear_acceleration.z + "\n";
        str += "Angular Acceleration: " + (int)msg.angular_velocity.x + " " + (int)msg.angular_velocity.y + " " + (int)msg.angular_velocity.z;

        imuText.text = str;
    }

    void OnVoltageCallback(Float32Msg msg)
    {
        voltageText.text = msg.data + " V";
    }

    void OnToFCallback(Float32MultiArrayMsg msg)
    {
        tofFrontLeftText.text = msg.data.GetValue(0) + " m";
        tofFrontRightText.text = msg.data.GetValue(1) + " m";
        tofRearLeftText.text = msg.data.GetValue(2) + " m";
        tofRearRightText.text = msg.data.GetValue(3) + " m";
    }

    void OnRPMCallback(Float32MultiArrayMsg msg)
    {
        for(int i = 0; i < msg.data.Length; i++)
        {
            if(Mathf.Abs((float)msg.data.GetValue(i)) > 0.05f)
            {
                lastMotionTime = time;
                firstImg = true;
                break;
            }
        }

        rpmFrontRightText.text = msg.data.GetValue(0) + " U/min";
        rpmRearRightText.text = msg.data.GetValue(1) + " U/min";
        rpmFrontLeftText.text = -(float)msg.data.GetValue(2) + " U/min";
        rpmRearLeftText.text = -(float)msg.data.GetValue(3) + " U/min";
    }

    void OnPoseCallback(PoseMsg msg)
    {
        string str = "Position: x:" + (int)msg.pose.position.x + ", y:" + (int)msg.pose.position.y + ", z:" + (int)msg.pose.position.z + "\n";
        str += "Rotation: x: " + (int)(msg.pose.orientation.x * Mathf.Rad2Deg) + ", y: " + (int)(msg.pose.orientation.y * Mathf.Rad2Deg) + ", z: " + (int)(msg.pose.orientation.z * Mathf.Rad2Deg) + ", w: " + (int)(msg.pose.orientation.w * Mathf.Rad2Deg);
        poseText.text = str;
    }

    void OnImageCallback(CompressedImageMsg msg)
    {
        if (msg.data.Length > 0)
        {
            if(target == null) target = new Texture2D(1, 1);

            target.LoadImage(msg.data);
            target.Apply();

            cameraImage.texture = target;

            // Differential Image
            if (!diffimg) return;

            if (Mathf.Abs(time.sec - lastMotionTime.sec) <= 1) return;

            if (firstImg)
            {
                //Debug.Log("First image.");
                oldImage = target;
                firstImg = false;
                return;
            }

            int motionPixels = 0;

            int w = target.width/8;
            int h = target.height/8;

            if(diff == null)
            diff = new Texture2D(w, h);

            for(int y = 0; y < diff.height; y++)
            {
                for(int x = 0; x < diff.width; x++)
                {
                    Color col = target.GetPixel(x * 8, y * 8);
                    Color oc = oldImage.GetPixel(x * 8, y * 8);
                    float r = Mathf.Abs(col.r - oc.r);
                    float g = Mathf.Abs(col.g - oc.g);
                    float b = Mathf.Abs(col.b - oc.b);
                    Color c = new Color(r, g, b);

                    if (c.maxColorComponent > imageMotionThreshold)
                    {
                        col = Color.green;
                        motionPixels++;
                    }

                    diff.SetPixel(x, y, col);
                }
            }

            diff.Apply();

            cameraDiffImage.texture = diff;

            if (motionPixels > motionPixelThreshold && Mathf.Abs(time.sec - lastImgMotionTime.sec) >= 10)
            {
                lastImgMotionTime = time;

                //string msgstr = "WGG's IOT has detected strong movement patterns!";
                string msgstr = motionMsgs[Random.Range(0, motionMsgs.Length)];

                Debug.Log(msgstr);

                StringMsg detectionMsg = new StringMsg(msgstr);
                ros.Publish(lifeRosTopic, detectionMsg);
            }

            oldImage = target;
        }
        else
        {
            Debug.LogError("Received empty image!");
        }
    }

    void OnScanCallback(LaserScanMsg msg)
    {
        // Get Polar Coordinats
        float[] ranges = msg.ranges;

        points = new List<Vector3>();
        cols = new List<Color>();

        // Convert to Cartesian coordinates and add them to points
        for (int r = 0; r < ranges.Length; r++)
        {
            if (ranges[r] > msg.range_min && ranges[r] < msg.range_max)
            {
                float rotation = /*(float)(-2*botRotation.x) + */(-(360.0f * r / ranges.Length)) * Mathf.PI / 180.0f;

                Vector3 v = new Vector3();
                v.x = ranges[r] * Mathf.Sin(rotation);
                v.z = ranges[r] * Mathf.Cos(rotation);
                points.Add(v);

                float red = 1.0f - (ranges[r] / msg.range_max);
                float green = ranges[r] / msg.range_max;
                cols.Add(new Color(red, green, 0));
            }
        }

        //points.Add(new Vector3());
        //cols.Add(Color.cyan);

        SetParticles(points.ToArray(), cols.ToArray());
    }

    public void SetParticles(Vector3[] positions, Color[] colors)
    {
        texColor = new Texture2D(positions.Length > (int)resolution ? (int)resolution : positions.Length, Mathf.Clamp(positions.Length / (int)resolution, 1, (int)resolution), TextureFormat.RGBAFloat, false);
        texPosScale = new Texture2D(positions.Length > (int)resolution ? (int)resolution : positions.Length, Mathf.Clamp(positions.Length / (int)resolution, 1, (int)resolution), TextureFormat.RGBAFloat, false);
        int texWidth = texColor.width;
        int texHeight = texColor.height;

        for (int y = 0; y < texHeight; y++)
        {
            for (int x = 0; x < texWidth; x++)
            {
                int index = x + y * texWidth;
                texColor.SetPixel(x, y, colors[index]);
                var data = new Color(positions[index].x, positions[index].y, positions[index].z, particleSize);
                texPosScale.SetPixel(x, y, data);
            }
        }

        texColor.Apply();
        texPosScale.Apply();

        particleCount = (uint)positions.Length;
        toUpdate = true;
    }
}
