using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using Windows.Kinect;
using static Unity.Burst.Intrinsics.X86;

public class BodyParticles : MonoBehaviour
{
  public Camera mainCamera;

  public new ParticleSystem particleSystem;

  public float depthScale = 65553.0f;

  public float remapMinDistance = 10.0f;
  
  public float spawnChance = 0.5f;
  
  public float delay = 0.1f;

  private UnityEngine.AudioSource mic;

  private KinectSensor _Sensor;
  private CoordinateMapper _Mapper;

  public GameObject CoordinateMapperManager;
  private CoordinateMapperManager _CoordinateMapperManager;

  [SerializeField]
  private ColorSourceManager _ColorManager;

  [SerializeField]
  private DepthSourceManager _DepthManager;

  private FrameDescription _depthFrameDesc;
  private FrameDescription _colorFrameDesc;

  DepthSpacePoint[] depthPoints;
  byte[] bodyIndexPoints;

  private ushort[] depthData;

  // Start is called before the first frame update
  void
  Start() {
    //mic = new UnityEngine.AudioSource();
    //mic.clip = Microphone.Start("", true, 10, 441000);
    //mic.Play();

    _Sensor = KinectSensor.GetDefault();

    if (_Sensor != null) {
      _Mapper = _Sensor.CoordinateMapper;
      FrameDescription frameDesc = _Sensor.DepthFrameSource.FrameDescription;

      if (!_Sensor.IsOpen) {
        _Sensor.Open();
      }
    }

    if (CoordinateMapperManager == null) {
      return;
    }

    _CoordinateMapperManager = CoordinateMapperManager.GetComponent<CoordinateMapperManager>();

    depthPoints = _CoordinateMapperManager.GetDepthCoordinates();
    bodyIndexPoints = _CoordinateMapperManager.GetBodyIndexBuffer();

    _depthFrameDesc = _Sensor.DepthFrameSource.FrameDescription;
    _colorFrameDesc = _Sensor.ColorFrameSource.FrameDescription;

    depthData = _DepthManager.GetData();

    StartCoroutine(StartShow());
  }

  // Update is called once per frame
  IEnumerator
  StartShow() {
    while (depthData == null) {
      yield return null;
    }

    while (true) {
      float[] depthValues = RefreshData(depthData);

      for (int y = 0; y < _depthFrameDesc.Height; y++) {
        for (int x = 0; x < _depthFrameDesc.Width; x++) {
          if (Random.Range(0.0f, 1.0f) > spawnChance) {
            continue;
          }

          Vector2 uv = new Vector2((float)x / _depthFrameDesc.Width, (float)y / _depthFrameDesc.Height);

          int colorIndex;
          {
            int colorWidth = (int)(uv.x * _colorFrameDesc.Width);
            int colorHeight = (int)(uv.y * _colorFrameDesc.Height);
            colorIndex = colorWidth + (colorHeight * _colorFrameDesc.Width);
          }

          int depthIndex = x + (y * _depthFrameDesc.Width);

          float depth = remap(depthValues[depthIndex], 0.0f, 0.01f, remapMinDistance, 400.0f);

          Vector3 viewportCoordinates = Vector3.zero;
          viewportCoordinates.x = uv.x * 1920.0f;
          viewportCoordinates.y = 1080.0f - (uv.y * 1080.0f);
          viewportCoordinates.z = depth;

          if (viewportCoordinates.z < 0.0f) {
            continue;
          }

          if (!IsSilhouette(colorIndex)) {
            continue;
          }

          Vector3 worldPoint = mainCamera.ScreenToWorldPoint(viewportCoordinates);

          ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams();
          emitParams.position = worldPoint;

          particleSystem.Emit(emitParams, 1);
        }
      }

      yield return new WaitForSeconds(delay);
    }
  }

  private float[]
  RefreshData(ushort[] depthData) {
    float[] depths = new float[depthData.Length];

    for (int y = 0; y < _depthFrameDesc.Height; y++) {
      for (int x = 0; x < _depthFrameDesc.Width; x++) {
        int index = (y * _depthFrameDesc.Width) + x;

        //double avg = GetAvg(depthData, x, y, frameDesc.Width, frameDesc.Height);
        //avg *= _DepthScale;

        depths[index] = depthData[index] == 0 ? -1.0f : (float)depthData[index] / depthScale;
      }
    }

    return depths;
  }

  private double
  GetAvg(ushort[] depthData, int x, int y, int width, int height) {
    double sum = 0.0;

    for (int y1 = y; y1 < y + 4; y1++) {
      for (int x1 = x; x1 < x + 4; x1++) {
        int cx = Mathf.Min(x1, width - 1);
        int cy = Mathf.Min(y1, height - 1);

        int fullIndex = (cy * width) + cx;

        if (depthData[fullIndex] == 0) {
          sum += 4500.0;
        }
        else {
          sum += depthData[fullIndex];
        }
      }
    }

    return sum / 16.0;
  }

  private bool
  IsSilhouette(int index) {
    if ((!float.IsInfinity(depthPoints[index].X) && !float.IsNaN(depthPoints[index].X) && depthPoints[index].X != 0) ||
         !float.IsInfinity(depthPoints[index].Y) && !float.IsNaN(depthPoints[index].Y) && depthPoints[index].Y != 0) {
      // We have valid depth data coordinates from our coordinate mapper.  Find player mask from corresponding depth points.
      float player = bodyIndexPoints[(int)depthPoints[index].X + (int)(depthPoints[index].Y * 512)];

      return player != 255;
    }

    return false;
  }

  private float
  remap(float s, float from1, float to1, float from2, float to2) {
    return ((s - from1) / ((to1 - from1) * (to2 - from2))) + from2;
  }
}
