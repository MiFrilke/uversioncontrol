// Copyright (c) <2017> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using UnityEngine;
using UnityEditor;
using System.Diagnostics;

namespace VersionControl
{
    public static class ProfilerUtilities
    {
        [Conditional("ENABLE_PROFILER")]
        public static void BeginSample(string name)
        {
            UnityEngine.Profiler.BeginSample(name);
        }

        [Conditional("ENABLE_PROFILER")]
        public static void BeginSample(string name, Object targetObject)
        {
            UnityEngine.Profiler.BeginSample(name, targetObject);
        }

        [Conditional("ENABLE_PROFILER")]
        public static void EndSample()
        {
            UnityEngine.Profiler.EndSample();
        }
    }
}