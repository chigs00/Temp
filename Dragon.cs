using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
using Watermelon;

public class Dragon : MonoBehaviour
{
    //public SplineContainer spline;
    //public float speed = 2f;
    //public bool movingForward = true;
    //[Range(0f, 1f)]
    //public float t = 0f;

    //public List<Transform> dragonBlocks;
    //public float spacing = 1f;
    //public bool isPaused = false;

    //void Update()
    //{
    //    if (isPaused)
    //    {
    //        return;
    //    }

    //    float delta = speed * Time.deltaTime / spline.CalculateLength();

    //    t += movingForward ? delta : -delta;
    //    t = Mathf.Clamp01(t);

    //    UpdateBlockPositions();
    //}

    //void UpdateBlockPositions()
    //{
    //    float spacing = this.spacing / dragonBlocks.Count;

    //    for (int i = 0; i < dragonBlocks.Count; i++)
    //    {
    //        float segmentT = movingForward ? t - i * spacing : t + i * spacing;
    //        segmentT = math.clamp(segmentT, 0f, 1f);

    //        float3 f3Pos, f3Tangent, f3Up;
    //        spline.Evaluate(segmentT, out f3Pos, out f3Tangent, out f3Up);

    //        // Convert to UnityEngine.Vector3
    //        Vector3 pos = (Vector3)f3Pos;
    //        Vector3 tangent = (Vector3)f3Tangent;
    //        Vector3 up = (Vector3)f3Up;

    //        Quaternion rot = Quaternion.LookRotation(tangent, up);

    //        dragonBlocks[i].position = pos;
    //        dragonBlocks[i].rotation = rot;
    //    }
    //}

    ////public void Reverse()
    ////{
    ////    movingForward = !movingForward;
    ////    Array.Reverse(dragonBlocks);

    ////    foreach (Transform block in dragonBlocks)
    ////    {
    ////        block.Rotate(0, 180f, 0); // Flip visuals if needed
    ////    }

    ////    t = 1f - t; // Mirror position on spline
    ////}

    //[ContextMenu("Test")]
    //private void Test()
    //{
    //    RemoveBlockAt(2);
    //}

    //public void RemoveBlockAt(int index)
    //{
    //    if (index < 0 || index >= dragonBlocks.Count)
    //        return;

    //    Transform removedBlock = dragonBlocks[index];
    //    Destroy(removedBlock.gameObject);

    //    // Remove from list
    //    dragonBlocks.RemoveAt(index);

    //    // Stop movement temporarily
    //    isPaused = true;

    //    // Start reverse animation coroutine
    //    StartCoroutine(MoveFrontBlocksToFillGap(index));
    //}

    //IEnumerator MoveFrontBlocksToFillGap(int removedIndex)
    //{
    //    float spacing = 1f / dragonBlocks.Count;
    //    float transitionTime = 0.5f;

    //    List<Vector3> startPositions = new List<Vector3>();
    //    List<Vector3> endPositions = new List<Vector3>();

    //    for (int i = 0; i < removedIndex; i++)
    //    {
    //        float fromT = GetTForIndex(i);
    //        float toT = GetTForIndex(i + 1); // Move forward

    //        spline.Evaluate(fromT, out float3 start, out float3 tan1, out float3 up1);
    //        spline.Evaluate(toT, out float3 end, out float3 tan2, out float3 up2);

    //        startPositions.Add((Vector3)start);
    //        endPositions.Add((Vector3)end);
    //    }

    //    float t = 0f;
    //    while (t < 1f)
    //    {
    //        t += Time.deltaTime / transitionTime;
    //        for (int i = 0; i < removedIndex; i++)
    //        {
    //            dragonBlocks[i].position = Vector3.Lerp(startPositions[i], endPositions[i], t);
    //        }
    //        yield return null;
    //    }

    //    // Snap to final positions
    //    for (int i = 0; i < removedIndex; i++)
    //    {
    //        float newT = GetTForIndex(i + 1); // Move forward
    //        spline.Evaluate(newT, out float3 finalPos, out float3 tan, out float3 up);
    //        dragonBlocks[i].position = (Vector3)finalPos;
    //    }

    //    // Resume movement
    //    isPaused = false;
    //}

    //float GetTForIndex(int i)
    //{
    //    float spacing = 1f / dragonBlocks.Count;
    //    return Mathf.Clamp01(t - i * spacing);
    //}


    [Header("Spline Setup")]
    public SplineContainer splineContainer;
    public float moveSpeed = 0.2f;

    [Header("Dragon Segments")]
    public List<Transform> initialBlocks;

    [Header("Block Spacing")]
    [Range(0.01f, 1f)]
    public float blockSpacing = 0.05f; // Normalized spline distance between blocks

    private List<DragonSegment> segments = new List<DragonSegment>();
    private bool isMoving = true;
    [SerializeField] float headT = 0f;

    void Start()
    {
        for (int i = 0; i < initialBlocks.Count; i++)
        {
            segments.Add(new DragonSegment
            {
                transform = initialBlocks[i],
                t = headT - i * blockSpacing
            });
        }
    }

    void Update()
    {
        if (!isMoving) return;

        headT += moveSpeed * Time.deltaTime;

        for (int i = 0; i < segments.Count; i++)
        {
            float t = headT - i * blockSpacing;
            t = Mathf.Clamp01(t);
            segments[i].t = t;

            splineContainer.Spline.Evaluate(t, out float3 pos, out float3 tan, out float3 up);
            segments[i].transform.position = pos;
            segments[i].transform.rotation = Quaternion.LookRotation(tan, up);
        }
    }

    public void RemoveBlockAt(int index)
    {
        if (index < 0 || index >= segments.Count)
            return;

        isMoving = false;
        Destroy(segments[index].transform.gameObject);
        //segments.RemoveAt(index);

        StartCoroutine(MoveFrontBlocksToFillGap(index));
    }

    IEnumerator MoveFrontBlocksToFillGap(int destroyedIndex)
    {
        float transitionTime = 2f;
        float elapsed = 0f;

        List<float> startT = new List<float>();
        List<float> targetT = new List<float>();

        // The number of blocks to move is from 0 to destroyedIndex - 1
        for (int i = 0; i < destroyedIndex; i++)
        {
            startT.Add(segments[i].t);
            targetT.Add(segments[i + 1].t); // 👈 Move to the NEXT segment’s position (shift forward)
        }

        // Pause movement during shift
        isMoving = false;

        while (elapsed < transitionTime)
        {
            float tLerp = elapsed / transitionTime;

            for (int i = 0; i < destroyedIndex; i++)
            {
                float lerpedT = Mathf.SmoothStep(startT[i], targetT[i], tLerp);
                splineContainer.Spline.Evaluate(lerpedT, out float3 pos, out float3 tan, out float3 up);

                segments[i].transform.position = pos;
                segments[i].transform.rotation = Quaternion.LookRotation(tan, up);
                segments[i].t = lerpedT; // Update segment t to the new position
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
        segments.RemoveAt(destroyedIndex);

        // Snap all front blocks to their targetTs
        for (int i = 0; i < destroyedIndex; i++)
        {
            segments[i].t = targetT[i];
            splineContainer.Spline.Evaluate(targetT[i], out float3 pos, out float3 tan, out float3 up);
            segments[i].transform.position = pos;
            segments[i].transform.rotation = Quaternion.LookRotation(tan, up);
        }

        // 🔄 Recalculate spacing from new head (Block1 is still head visually, but now aligned with Block2’s old t)
        float newHeadT = segments[0].t /*+ blockSpacing*/;
        //for (int i = 0; i < segments.Count; i++)
        //{
        //    float newT = newHeadT - i * blockSpacing;
        //    newT = Mathf.Clamp01(newT);
        //    segments[i].t = newT;

        //    splineContainer.Spline.Evaluate(newT, out float3 pos, out float3 tan, out float3 up);
        //    segments[i].transform.position = pos;
        //    segments[i].transform.rotation = Quaternion.LookRotation(tan, up);
        //}

        headT = newHeadT;
        isMoving = true;
    }

    public void RemoveMultipleBlocksAt(List<int> indexes)
    {
        if (indexes == null || indexes.Count == 0)
            return;

        indexes.Sort(); // Make sure in ascending order
        int firstDestroyedIndex = indexes[0];

        // Destroy visuals
        foreach (int index in indexes)
        {
            if (index >= 0 && index < segments.Count)
                Destroy(segments[index].transform.gameObject);
        }

        StartCoroutine(MoveFrontBlocksToFillMultipleGaps(indexes));
    }

    IEnumerator MoveFrontBlocksToFillMultipleGaps(List<int> destroyedIndexes)
    {
        float transitionTime = 0.4f;
        float elapsed = 0f;

        int fillCount = destroyedIndexes.Count;
        int startIndex = destroyedIndexes[0];

        List<float> startT = new List<float>();
        List<float> targetT = new List<float>();

        // Get T values to move front blocks into destroyed block positions
        for (int i = 0; i < fillCount; i++)
        {
            int frontIndex = startIndex - fillCount + i;

            if (frontIndex < 0 || (startIndex + i) >= segments.Count)
                continue;

            startT.Add(segments[frontIndex].t);
            targetT.Add(segments[startIndex + i].t);
        }

        isMoving = false;

        while (elapsed < transitionTime)
        {
            float tLerp = elapsed / transitionTime;

            for (int i = 0; i < startT.Count; i++)
            {
                float lerpedT = Mathf.SmoothStep(startT[i], targetT[i], tLerp);
                splineContainer.Spline.Evaluate(lerpedT, out float3 pos, out float3 tan, out float3 up);

                int frontIndex = startIndex - fillCount + i;
                segments[frontIndex].transform.position = pos;
                segments[frontIndex].transform.rotation = Quaternion.LookRotation(tan, up);
                segments[frontIndex].t = lerpedT;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Snap to final T values
        for (int i = 0; i < startT.Count; i++)
        {
            int frontIndex = startIndex - fillCount + i;
            segments[frontIndex].t = targetT[i];

            splineContainer.Spline.Evaluate(targetT[i], out float3 pos, out float3 tan, out float3 up);
            segments[frontIndex].transform.position = pos;
            segments[frontIndex].transform.rotation = Quaternion.LookRotation(tan, up);
        }

        // Remove destroyed segments from list (from highest to lowest index)
        destroyedIndexes.Sort((a, b) => b.CompareTo(a));
        foreach (int index in destroyedIndexes)
        {
            if (index >= 0 && index < segments.Count)
                segments.RemoveAt(index);
        }

        // Recalculate spacing and headT
        float newHeadT = segments[0].t + blockSpacing;
        for (int i = 0; i < segments.Count; i++)
        {
            float newT = newHeadT - i * blockSpacing;
            newT = Mathf.Clamp01(newT);

            segments[i].t = newT;
            splineContainer.Spline.Evaluate(newT, out float3 pos, out float3 tan, out float3 up);
            segments[i].transform.position = pos;
            segments[i].transform.rotation = Quaternion.LookRotation(tan, up);
        }

        headT = newHeadT;
        isMoving = true;
    }



    [System.Serializable]
    public class DragonSegment
    {
        public Transform transform;
        public float t;
    }
    [ContextMenu("Test")]
    private void Test()
    {
        RemoveMultipleBlocksAt(new List<int> { 3, 4, 5 });
    }
}
