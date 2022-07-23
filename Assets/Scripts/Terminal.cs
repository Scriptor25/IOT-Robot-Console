using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class Terminal : MonoBehaviour
{
    public string ExecuteProcessTerminal(string argument)
    {
        string output = "";

        try
        {
            //UnityEngine.Debug.Log("============== Start Executing [" + argument + "] ===============");
            ProcessStartInfo startInfo = new ProcessStartInfo("/bin/bash")
            {
                WorkingDirectory = "/",
                UseShellExecute = false,
                RedirectStandardOutput = true
            };

            Process myProcess = new Process
            {
                StartInfo = startInfo
            };

            myProcess.StartInfo.Arguments = argument;
            myProcess.Start();

            output = myProcess.StandardOutput.ReadToEnd();
            //UnityEngine.Debug.Log("Result for [" + argument + "] is : \n" + output);
            myProcess.WaitForExit();
            //UnityEngine.Debug.Log("============== End ===============");
        }
        catch (Exception e)
        {
            //UnityEngine.Debug.Log(e);
            output = e.Message;
        }

        return output;
    }
}
