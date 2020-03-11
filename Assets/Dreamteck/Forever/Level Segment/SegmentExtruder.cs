using UnityEngine;
using System.Threading;
using System.Collections.Generic;

namespace Dreamteck.Forever
{
    public class SegmentExtruder : MonoBehaviour
    {
        public int framesBetweenBuilds = 2;
        public bool multithreaded = true;
        private int framesPassed = 0;
        private Thread buildThread;
        private List<LevelSegment> buildQueue = new List<LevelSegment>();
        private object locker = new object();
        public static SegmentExtruder instance;

        private volatile bool postBuild = false;
        private LevelSegment buildSegment = null;

        // Use this for initialization
        void Awake()
        {
            instance = this;
        }

        // Update is called once per frame
        void Update()
        {
            if (multithreaded)
            {
                bool startThread = buildThread == null;
                if (!startThread) startThread = !buildThread.IsAlive;
                if (startThread)
                {
                    if (buildThread != null) Debug.Log("Thread restarted");
                    buildThread = new Thread(BuildThread);
                    buildThread.Start();
                }
            }

            if (postBuild)
            {
                buildSegment.OnPostExtrude();
                buildSegment = null;
                postBuild = false;
                framesPassed = 0;
            }


            if (buildSegment == null && framesPassed >= framesBetweenBuilds)
            {
                if (buildQueue.Count > 0)
                {
                    if (LevelSegment.generationState == LevelSegment.GenerationState.Free || LevelSegment.generationState == LevelSegment.GenerationState.Idle)
                    {
                        buildSegment = buildQueue[0];
                        buildSegment.OnBeforeExtrude();
                        buildQueue.RemoveAt(0);
                        if (multithreaded) buildThread.Interrupt();
                        else if (buildSegment != null)
                        {
                            buildSegment.Extrude();
                            postBuild = true;
                        }
                    }
                }
            }
            if (framesPassed < framesBetweenBuilds) framesPassed++;
        }

        void OnDisable()
        {
            if (buildThread != null)
            {
                buildThread.Abort();
                buildThread = null;
            }
        }

        void OnDestroy()
        {
            if (buildThread != null)
            {
                buildThread.Abort();
                buildThread = null;
            }
        }

        void OnApplicationQuit()
        {
            if (buildThread != null)
            {
                buildThread.Abort();
                buildThread = null;
            }
        }

        public void Stop()
        {
            if (buildThread != null && buildThread.IsAlive) buildThread.Abort();
            buildQueue.Clear();
            buildSegment = null;
            postBuild = false;
        }

        public void AddToBuildQueue(LevelSegment input)
        {
            buildQueue.Add(input);
        }

        void BuildThread()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(Timeout.Infinite);
                }
                catch (ThreadInterruptedException)
                {
                    lock (locker)
                    {
                        if (buildSegment != null)
                        {
                            try
                            {
                                buildSegment.Extrude();
                                postBuild = true;
                            }
                            catch (UnityException ex)
                            {
                                Debug.Log("Segment builder Unity extepion in thread: " + ex.Message);
                            }
                            catch (System.Exception ex)
                            {
                                Debug.Log("Segment builder System extepion in thread: " + ex.Message);
                            }
                        }
                    }
                }
                catch (ThreadAbortException)
                {
                    break;
                }
            }
        }
    }
}
