using UnityEngine;

namespace DongbaPrototype { public partial class ObliqueForest {
    [Header("Story opening camera")]
    [Range(6f,14f)] public float OpeningSurveySeconds = 9.4f;
    float openingSurveyAge;
    bool OpeningSurveyActive => StoryModeEnabled && fatherIntroActive && openingSurveyAge < OpeningSurveySeconds;

    void ResetOpeningSurvey() { openingSurveyAge = 0; }
    void UpdateOpeningSurvey(float dt) {
        if (OpeningSurveyActive) openingSurveyAge = Mathf.Min(OpeningSurveySeconds, openingSurveyAge + dt);
    }
    static float OpeningEase(float t) {
        t = Mathf.Clamp01(t);
        return t*t*t*(t*(t*6f-15f)+10f);
    }
    void OpeningSurveyPose(out Vector3 center, out float zoom) {
        // Survey the width of the existing map without exposing the paper plane's edge.
        // Keep the ground projection inside its 92 x 42 bounds at different aspect ratios.
        float wide = Mathf.Min(12.6f, 19.7f * Mathf.Abs(cam.transform.up.z), 43f / cam.aspect);
        float side = Mathf.Max(0, 44f - wide * cam.aspect);
        Vector3 west = new Vector3(-side, 0, 0), east = new Vector3(side, 0, 0);
        float t = openingSurveyAge / Mathf.Max(.1f, OpeningSurveySeconds);
        if (t < .55f) {
            center = Vector3.Lerp(west, east, OpeningEase(Mathf.InverseLerp(.07f,.55f,t)));
            zoom = wide;
        } else {
            float settle = OpeningEase(Mathf.InverseLerp(.58f,.96f,t));
            center = Vector3.Lerp(east, HomeStoryCenter, settle);
            zoom = Mathf.Lerp(wide, HomeStoryZoom, settle);
        }
    }
}}
