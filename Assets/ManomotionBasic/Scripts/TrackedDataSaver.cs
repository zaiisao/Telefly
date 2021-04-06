using System;
using UnityEngine;
using TMPro;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine.UI;
using System.Net;

public class TrackedDataSaver : MonoBehaviour
{
    public RectTransform targetCursor;
    public TextMeshProUGUI positionValue;
    public GameObject trackingGizmo;
    public GameObject handTracking;
    public GameObject ipInput;
    public GameObject newCharacterMenu;

    private float palmCenterX;
    private float palmCenterY;
    private float palmCenterZ;

    private float lastPositionX;
    private float lastPositionY;
    private float lastPositionZ;

    private float deltaX;
    private float deltaY;
    private float deltaZ;

    private Thread clientReceiveThread;
    private TcpClient socketConnection;
    public ManomotionManager manoManager;

    [Serializable]
    public class HandGesture
    {
        public float trackedX;
        public float trackedY;
        public float trackedZ;
        public float deltaX;
        public float deltaY;
        public float deltaZ;
    }

    private string serverIP;
    private bool isRecording = false;
    private Button recordButton;

    // Start is called before the first frame update
    void Start()
    {
        //ReceiveIPFromUser();
        StartNewCharacterMenu();

        lastPositionX = 0.0f;
        lastPositionY = 0.0f;
        lastPositionZ = 0.0f;
    }

    private void Update() {
        if (handTracking.activeInHierarchy) {
            palmCenterX = targetCursor.localPosition.x;
            palmCenterY = targetCursor.localPosition.y;
            palmCenterZ = targetCursor.localPosition.z;
            
            HandGesture myGesture = new HandGesture();
            myGesture.trackedX = palmCenterX;
            myGesture.trackedY = palmCenterY;
            myGesture.trackedZ = palmCenterZ;
            myGesture.deltaX = palmCenterX - lastPositionX;
            myGesture.deltaY = palmCenterY - lastPositionY;
            myGesture.deltaZ = palmCenterZ - lastPositionZ;

            Debug.Log(JsonUtility.ToJson(myGesture));

            positionValue = trackingGizmo.transform.Find("PositionValue").gameObject.GetComponent<TextMeshProUGUI>();
            positionValue.text = "[X:" + palmCenterX + " , Y: " + palmCenterY + " , Z: " + palmCenterZ + "] \r\n (dX: " + myGesture.deltaX + " ,dY: " + myGesture.deltaY + " , dZ: " + myGesture.deltaZ + ")";

            if (isRecording) {
                SendGestureToServer(myGesture);
            }
        }

        lastPositionX = palmCenterX;
        lastPositionY = palmCenterY;
        lastPositionZ = palmCenterZ;
    }

    private void StartNewCharacterMenu() {
        GameObject butterflyObject = GameObject.FindGameObjectWithTag("butterfly");
        GameObject butterflyModel = butterflyObject.transform.Find("Character_Butterfly_Model").gameObject;

        var mainCanvas = newCharacterMenu.transform.Find("Canvas");
        var colors = mainCanvas.transform.Find("Colors");
        Button[] colorButtons = colors.GetComponentsInChildren<Button>();
        for (int i = 0; i < colorButtons.Length; i++) {
            Image buttonImage = colorButtons[i].GetComponent<Image>();
            colorButtons[i].onClick.AddListener(() => {
                butterflyModel.GetComponent<Renderer>().material.SetColor("_Color", buttonImage.color);
            });
        }

        var startButtonObject = mainCanvas.transform.Find("StartButton");
        Button startButton = startButtonObject.GetComponent<Button>();
        startButton.onClick.AddListener(RunTracking);
    }

    private void ReceiveIPFromUser() {
        handTracking.SetActive(false);
        newCharacterMenu.SetActive(false);
        ipInput.SetActive(true);

        Canvas ipInputCanvas = ipInput.GetComponentInChildren<Canvas>();
        Button connectButton = ipInputCanvas.GetComponentInChildren<Button>();
        connectButton.onClick.AddListener(OnConnectButtonClick);
    }

    private void OnConnectButtonClick() {
        Canvas canvas = ipInput.GetComponentInChildren<Canvas>();

        InputField ipField = canvas.transform.GetComponentInChildren<InputField>();
        Text errorMessage = canvas.transform.Find("ErrorMessage").GetComponent<Text>();

        string ip = ipField.text;
        if (!IPAddress.TryParse(ip, out _)) {
            errorMessage.GetComponentInChildren<Text>().text = "올바르지 않은 주소입니다.";
            ipField.text = "";
        } else {
            serverIP = ip;
            RunTracking();
        }
    }

    private void RunTracking() {
        ipInput.SetActive(false);
        newCharacterMenu.SetActive(false);
        handTracking.SetActive(true);

        recordButton = GameObject.Find("RecordButton").GetComponent<Button>();
        recordButton.onClick.AddListener(OnRecordButtonClick);

        clientReceiveThread = new Thread(new ThreadStart(ListenForData));
        clientReceiveThread.IsBackground = true;
        clientReceiveThread.Start();
    }

    private void OnRecordButtonClick() {
        isRecording = !isRecording;
        recordButton.GetComponentInChildren<Text>().text = isRecording ? "Stop Recording" : "Start Recording";
    }

    private void ListenForData() {
        try {
            socketConnection = new TcpClient(serverIP, 10001);
            Byte[] bytes = new Byte[1024];
            while (true) {
                using (NetworkStream stream = socketConnection.GetStream()) {
                    int length;
                    while ((length = stream.Read(bytes, 0, bytes.Length)) != 0) {
                        var incomingData = new byte[length];
                        Array.Copy(bytes, 0, incomingData, 0, length);
                        string serverMessage = Encoding.ASCII.GetString(incomingData);
                        Debug.Log("server message received as: " + serverMessage);
                    }
                }
            }
        } catch (SocketException exception) {
            Debug.Log("Socket exception: " + exception);
        }
    }

    private void SendGestureToServer(HandGesture gesture) {
        if (socketConnection == null) {
            return;
        }

        try {
            NetworkStream stream = socketConnection.GetStream();
            if (stream.CanWrite) {
                float[] floatArray = new float[] {gesture.trackedX, gesture.trackedY, gesture.trackedZ, gesture.deltaX, gesture.deltaY, gesture.deltaZ};
                byte[] byteArray = new byte[floatArray.Length*sizeof(float)];
                Buffer.BlockCopy(floatArray, 0, byteArray, 0, byteArray.Length);

                stream.Write(byteArray, 0, byteArray.Length);
                Debug.Log("Client sent his message - should be received by server");
            }
        } catch (SocketException exception) {
            Debug.Log("Socket exception: " + exception);
        }
    }
}
