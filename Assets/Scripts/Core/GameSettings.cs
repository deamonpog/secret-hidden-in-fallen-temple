using System;
using UnityEngine;

// Stores player preferences that persist between sessions.
public static class GameSettings
{
    private const string RotateModeKey = "settings.cameraRotateMode";
    private const CameraRotateMode DefaultRotateMode = CameraRotateMode.LockedCursor;

    // Raised after the camera rotate mode has been changed and saved.
    public static event Action RotateModeChanged;

    // How the player rotates the camera. Setting it saves the value immediately.
    public static CameraRotateMode RotateMode
    {
        get
        {
            int stored = PlayerPrefs.GetInt(RotateModeKey, (int)DefaultRotateMode);
            return Enum.IsDefined(typeof(CameraRotateMode), stored)
                ? (CameraRotateMode)stored
                : DefaultRotateMode;
        }
        set
        {
            if (value == RotateMode)
                return;

            PlayerPrefs.SetInt(RotateModeKey, (int)value);
            PlayerPrefs.Save();
            RotateModeChanged?.Invoke();
        }
    }
}
