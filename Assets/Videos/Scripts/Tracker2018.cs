using UnityEngine;
using UnityEditor;
using UnityEngine.Video;
using UnityEngine.Playables;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Text;
using System.Globalization;


/*! \An example tracking script that logs the world position of the object it is attached to.
 *
 *  Set the logs per second and where to output the saved positions.  These can be read by the Heatmap class and turned into a Heatmap.
 */
public class Tracker2018: MonoBehaviour
{

	public class SphereCoordinates
	{
		public float phi;     // entspricht dem geografischen Längengrad  (0 <= phi <= 360)   von innen geehen nach rechts 
		public float theta;   // entspricht dem geografischen Breitengrad (270(Nordpol) <= theta <= 360 ) ^ (0 <= theta <= 90(Suedpol)   

		public SphereCoordinates(float phi, float theta)
		{
			this.phi = phi;
			this.theta = theta;
		}
	}

	public enum BaseType {video, animation}
	public enum SourceType {head, eyes}

	private int version = 31;

	public BaseType Type = BaseType.video;					// case: video || animation

	public GameObject TimeBase;                             // needed to determine the seek time
															// for BaseType,video: (we watch a movie): this Gameobject must be contain VideoPlayer script
                            								// for BaseType,anination: (we watch a animation): this Gameobject must be contain the PlayableDirector

	public SourceType Source = SourceType.head;				// case: headtracking || eyetracking

    public float LogsPerSecond = 1f;                     	// default: one log per second
    public string HeatmapFile = "TrackPoints";          	// default log file name
    // ToDo
	public Boolean UseMovieName = true;                   	// use the movie name as log file name
    public Boolean AddDate2File = true;                   	// add the current date and time to the log file nam
    public string Tag = "";                     			// an user defined string as tag, if "" => take the m_startTime string as Tag
    public string StartTime;                               	// out only: the start time as formated string
    public string SaveTime;                                	// out only: the start time as formated string
    public double CurrentSeekPosition;                     	// out only: the current seek position of the movie
	public float CurrentTimePosition = 0f;                  // out only: the current time position of the animation
    public int NumberOfPoints;                          	// out only: the current number of logged points
    public int PointsPerSave = 1000;                    	// save after every <PointsPerSave> tracking points, defaults to 1000

    public string HardcopyTimes;
    public Camera HCCamera;

    private string m_heatmapPath;
    private float m_logDelay;
    private float m_timer = 0f;
    private Boolean m_trackingStarted = false;
    private DateTime m_startTime;
	private string m_movieName = "NoName";    

    private VideoPlayer m_script;							// for case: we watch a movie
    private PlayableDirector m_pd;							// dor case: we watch an animation

    private StringBuilder m_trackString;
    private int m_partcnt = 0;

    private bool m_errorflag = false;

    private float[] m_hcTimes;
	private int m_hcPtr;
	private int m_hcMax;
    private StringBuilder m_hcYRotationString;


    public void Start()
    {
        Debug.Log("UNITY360 >>>Start Version: " + version);

		// converting string HardcopyTimes like "3.0 4.6 7.1" into an array of floats
		if (HardcopyTimes != null) {
			char[] charSeparators = new char[] {' '};
			string[] hcs = HardcopyTimes.Split(charSeparators,StringSplitOptions.RemoveEmptyEntries);
			m_hcMax = hcs.Length;
			Debug.Log("UNITY360 >>>HC Max : " + m_hcMax);
			
			m_hcTimes = new float[m_hcMax];
			for (int i = 0; i < m_hcMax; i++) {
				m_hcTimes[i] = float.Parse(hcs[i]);
			}
            m_hcYRotationString = new StringBuilder("");
        }

        m_heatmapPath = Application.persistentDataPath;
        m_logDelay = 1f / LogsPerSecond;

		if (Type == BaseType.video) {
			m_script = TimeBase.GetComponent<VideoPlayer> ();
			m_movieName = System.IO.Path.GetFileName(m_script.clip.name); //remove the path, use the file name only
		} else if (Type == BaseType.animation) {  
			if (TimeBase != null) {
				m_pd = TimeBase.GetComponent<PlayableDirector> ();
			} else {
				Debug.Log("UNITY360  ... Using Internal Time (CurrentTimePosition)");
			}
		} else {
            Debug.LogError("Neither Video Player nor Time line was set");
		}
			
        Debug.Log("UNITY360  ... Movie File Name = " + m_movieName);
        if (UseMovieName == true)
        {
            HeatmapFile = System.IO.Path.GetFileNameWithoutExtension(m_movieName);  // heatmap file name = movie name without extension
            Debug.Log("UNITY360 ... Heatmap File Name = " + m_movieName);
        }
			
        Debug.Log("UNITY360  ... Using Raw Format");
		m_trackString = new StringBuilder("version: " + version + "\n");
		m_trackString.Append("movie: " + m_movieName + "\n");
		m_trackString.Append("logDelay: " + (int)(m_logDelay * 1000) + "\n");
    }

    public void Update()
    {
        bool _isPlaying;

        m_timer += Time.deltaTime;
		CurrentTimePosition += Time.deltaTime;

        if (Type == BaseType.video)
            _isPlaying = m_script.isPlaying;
        else
			if (m_pd != null) {
				_isPlaying = ((m_pd.state == PlayState.Playing) && (m_pd.time > 0));
			} else {
				_isPlaying = true;
			}

        if (m_trackingStarted == false && _isPlaying)
        {
            // this is called only one time at the begin of movie
            Debug.Log("UNITY360 >>>>>>>>>>>>>> Update(): PLaying State = PLAYING");
            m_trackingStarted = true;
            m_startTime = DateTime.Now;
            StartTime = m_startTime.ToString("yyyy-MM-dd_HH:mm:ss");
					
			if (Type == BaseType.video) {
				CurrentSeekPosition = m_script.time;
			} else if (Type == BaseType.animation) {  
				if (m_pd != null) {
					CurrentSeekPosition = m_pd.time;
				} else {
					CurrentSeekPosition = (double)CurrentTimePosition;
				}
			} 

            if (AddDate2File == true)
                HeatmapFile = HeatmapFile + "_" + m_startTime.ToString("yyyyMMddHHmmss");
            if (Tag == "")
                Tag = m_startTime.ToString("yyyyMMddHHmmss");
			Tag = (Source == SourceType.head) ? ("H_" + Tag) :  ("E_" + Tag);

            m_trackString.Append("tag: " + Tag + "\n");
            m_trackString.Append("time: " + StartTime + "\n\n");
            m_trackString.Append("# seekPos  x  y\n");

            m_timer = 0f;
            LogIt(gameObject.transform, CurrentSeekPosition);
        }
			
		if (Type == BaseType.video) {
            _isPlaying = !(m_script.time >= m_script.clip.length);
        } else {
			if (m_pd != null) {
				_isPlaying = !((m_pd.state == PlayState.Paused) && (m_pd.time == 0));
			} else {
				_isPlaying = true;
			}
		}

        if (m_trackingStarted == true && !_isPlaying)
        {
            // this is called only one time at the end of movie
            Debug.Log("UNITY360 >>>>>>>>>>>>>> Update(): Playing State = END");
            m_trackingStarted = false;
        }

        if (m_trackingStarted == true && m_timer > m_logDelay)
        {
            m_timer = 0f;
			if (Type == BaseType.video) {
				CurrentSeekPosition = m_script.time;
			} else if (Type == BaseType.animation) {  
				if (m_pd != null) {
					CurrentSeekPosition = m_pd.time;
				} else {
					CurrentSeekPosition = (double)CurrentTimePosition;
				}
			}

            //Debug.Log("UNITY360 >>>>>>>>>>>>>> Update(): seekPos = " + CurrentSeekPosition);
			LogIt(gameObject.transform, CurrentSeekPosition);

			// checking for hardcopy
            if (m_hcPtr < m_hcMax && CurrentTimePosition >= m_hcTimes[m_hcPtr]) {
                Debug.Log("UNITY360 ... HC at " + m_hcTimes[m_hcPtr]);

                // to store the y Rotation
                float y = GetComponentInParent<Camera>().transform.eulerAngles.y;
                Debug.Log("UNITY360 ... HC at rotY " + y);
              
                // make hardcopy
                //byte[] moviebytes = I360Render.Capture(1024, true, null);
                // save Bytes and y Rotation
                SaveSnapshot(null, m_hcPtr, y);
				// set ptr to next time for hardcopy
                m_hcPtr++;

            }
        }
    }

    public void OnDisable()
    {
        Debug.Log("UNITY360 >>>>>>>>>>>>>> OnDisable()");
        // final save the logs and write the output file(s)
        // some logs are already written in LogIt() (every <PointsPerSave> calls by the Update function
        // otherwise we have a string, witch could be to big for the android
        SaveTime = DateTime.Now.ToString("yyyy-MM-dd_HH:mm:ss");
        FinalSaveLog(m_heatmapPath + "/" + HeatmapFile);

        //SaveMovie();
    }

    // -------------------------------------------------------------------------------

	private void LogIt(Transform transform, double seekPosition)
	{
		if (Source == SourceType.head)
			LogHead (transform.rotation, seekPosition);
		else if (Source == SourceType.eyes)
			LogEyePosion (transform, seekPosition);
	}

    private void LogHead(Quaternion rotation, double seekPosition)
    {

		//Debug.Log("UNITY360 LogHead(): X = " + rotation.eulerAngles.x + ", Y = " + rotation.eulerAngles.y);

		// converting
		float phi = rotation.eulerAngles.y;
		float theta = rotation.eulerAngles.x;

		LogPosition (phi, theta, seekPosition);
    }
		
	private void LogEyePosion(Transform transform, double seekPosition)
	{

		// switching
		float x = transform.position.z;
		float y = transform.position.x;
		float z = transform.position.y;

		//Debug.Log("UNITY360 LogEyePosion(): X = " + x + ", Y = " + y + ", Z = " + z);

		// converting
		float phi = CartCoordinates2Phi(x, y, z);
		float theta = CartCoordinates2Theta(x, y, z);

		NumberOfPoints++;
		LogPosition(phi, theta, seekPosition);

	}

	private void LogPosition(float phi, float theta, double seekPosition)
	{

        //Debug.Log("UNITY360 LogEyePosion2RawString(): i = " + NumberOfPoints + ", Pos = " + seekPosition);

        //string timePos = String.Format("{0:0.000}", seekPosition);
        //m_trackString.Append(timePos + " " + theta + " " + phi + "\n");

        string timePos = seekPosition.ToString("F3", CultureInfo.InvariantCulture);
        string thetaString = theta.ToString("F", CultureInfo.InvariantCulture);
        string phiString = phi.ToString("F", CultureInfo.InvariantCulture);
        m_trackString.Append(timePos + " " + thetaString + " " + phiString + "\n");

        NumberOfPoints++;
		if (NumberOfPoints % PointsPerSave == 0)
		{
			SaveLog();
		}
	}

    private void SaveLog()
    {

        Debug.Log("UNITY360 SaveRotationLog(): saving data, Part Count = " + m_partcnt);

        string file = m_heatmapPath + "/" + HeatmapFile + ".raw";
        m_partcnt++;

        System.IO.File.AppendAllText(file, m_trackString.ToString());
        m_trackString = m_trackString.Remove(0, m_trackString.Length);
    }



    private void FinalSaveLog(string path)
    {
        Debug.Log("UNITY360 >>>>>>>>>>>>>> FinalSaveLog(): Path = " + path + ", L = " + NumberOfPoints);

        // save vtt/raw file, if we still have to save some points
        if (NumberOfPoints > m_partcnt * PointsPerSave)
        {
            Debug.Log("UNITY360 ... FinalSaveLog(): saving vtt/raw: remaining " + (NumberOfPoints - m_partcnt * PointsPerSave) + " points to save");
            SaveLog();
        }

        // save y rotation 
        string file = m_heatmapPath + "/" + HeatmapFile + "_YRot.text";
        System.IO.File.AppendAllText(file, m_hcYRotationString.ToString());

    }

    private void SaveSnapshot(byte[] movieBytes, int number, float y)
    {
        Debug.Log("UNITY360 >>>>>>>>>>>>>> SaveMovie() at time number: " + number + " : Path = " + m_heatmapPath);

        // save 
        string file = m_heatmapPath + "/snapshot_" + number + ".jpg";
        //System.IO.File.WriteAllBytes(file, movieBytes);

        string yString = y.ToString("F3", CultureInfo.InvariantCulture);
        m_hcYRotationString.Append(number + " " + yString + "\n");

    }

    // helper functions ---------------------------------------------------------------------

    // converts Cartesian Coordinates (x,y,z) to Spherical Coordinates (phi, theta)
    // asuming:
    // 1) x-y-Ebene ist die Äquator-Ebene: Nordpol: (0,0,r), Suedpol: (0,0,-r)
    // => Nordpol: (0,0,r) -> (270, 0); Suepol: (0,0,-r) -> (90, 0);
    // 2) Nullpunkt (Mttelpubnkt der Leinwand) ist (r,0,0)
    // => Nullpunkt ist (r,0,0) -> (0,0)
    private SphereCoordinates CartCoordinates2SphereCoordinates (float x, float y, float z)
	{
		float r = Mathf.Sqrt (x * x + y * y + z * z);
		float t = Mathf.Acos(z / r);   // hier ist 0 <= t <= pi, aber 270(Nordpol) <= theta <= 360 ) ^ (0 <= theta <= 90(Suedpol)  
		// => theta = t + (3/2)*pi, falls theta > 2pi => theta = theta - 2pi
		float thetaRad = t + Mathf.PI * ((float)3 / (float)2);
		if (thetaRad >= 2 * Mathf.PI)
		{
			thetaRad -= 2 * Mathf.PI;
		}

		float p = Mathf.Atan2(y, x);    // hier ist -pi <= p <= pi, aber (0 <= phi <= 360) 
		// => phi = p + 2pi falls p < 0
		float phiRad = p;
		if (phiRad < 0)
		{
			phiRad += 2 * Mathf.PI;
		}

		// Umrechnen Rad in Grad
		float theta = Mathf.Rad2Deg * thetaRad;
		float phi = Mathf.Rad2Deg * phiRad;

		return new SphereCoordinates(phi, theta);
	}

	private float CartCoordinates2Phi(float x, float y, float z)
	{

		float p = Mathf.Atan2(y, x);    // hier ist -pi <= p <= pi, aber (0 <= phi <= 360) 
		// => phi = p + 2pi falls p < 0
		float phiRad = p;
		if (phiRad < 0)
		{
			phiRad += 2 * Mathf.PI;
		}

		// Umrechnen Rad in Grad
		float phi = Mathf.Rad2Deg * phiRad;

		return phi;
	}

	private float CartCoordinates2Theta(float x, float y, float z)
	{

		float r = Mathf.Sqrt(x * x + y * y + z * z);
		float t = Mathf.Acos(z / r);   // hier ist 0 <= t <= pi, aber 270(Nordpol) <= theta <= 360 ) ^ (0 <= theta <= 90(Suedpol)  
		// => theta = t + (3/2)*pi, falls theta > 2pi => theta = theta - 2pi
		float thetaRad = t + Mathf.PI * ((float)3 / (float)2);
		if (thetaRad >= 2 * Mathf.PI)
		{
			thetaRad -= 2 * Mathf.PI;
		}

		// Umrechnen Rad in Grad
		float theta = Mathf.Rad2Deg * thetaRad;

		return theta;
	}
		
}

[CustomEditor(typeof(Tracker2018))]
public class Tracker2018Editor : Editor
{
	override public void OnInspectorGUI()
	{
		base.OnInspectorGUI();
		EditorGUILayout.LabelField("(Below this object)");
	}
}

