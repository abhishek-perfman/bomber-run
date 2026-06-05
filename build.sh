#!/bin/bash
set -e

# Path to the Windows Unity executable (to run directly from WSL)
UNITY_PATH="/mnt/c/Program Files/Unity/Hub/Editor/6000.4.6f1/Editor/Unity.exe"

echo "Checking Unity Editor path..."
if [ ! -f "$UNITY_PATH" ]; then
    echo "Error: Unity Editor not found at: $UNITY_PATH"
    echo "Please update the UNITY_PATH variable in build.sh to match your Unity Editor installation."
    exit 1
fi

PROJECT_PATH_WIN=$(wslpath -w "$PWD")
echo "Unity Project Path (Windows): $PROJECT_PATH_WIN"

echo "============================================="
echo "🎮 Starting WebGL Build..."
echo "============================================="
"$UNITY_PATH" -batchmode -quit -projectPath "$PROJECT_PATH_WIN" -buildTarget WebGL -executeMethod BuildScript.BuildWebGL -logFile build_webgl.log

echo "WebGL Build Completed! (Log: build_webgl.log)"

echo "============================================="
echo "🤖 Starting Android Build..."
echo "============================================="
"$UNITY_PATH" -batchmode -quit -projectPath "$PROJECT_PATH_WIN" -buildTarget Android -executeMethod BuildScript.BuildAndroid -logFile build_android.log

echo "Android Build Completed! (Log: build_android.log)"
echo "Builds are available under the Builds/ directory."
