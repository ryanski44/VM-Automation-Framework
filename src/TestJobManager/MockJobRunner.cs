using System;
using System.Collections.Generic;
using System.Text;
using JobManagerInterfaces;

namespace TestJobManager
{
    public class MockJobRunner : JobRunner
    {
        public ExecutionResult Execute(JobClientInterface jci)
        {
            ExecutionResult er = new ExecutionResult();
            er.Success = jci.GetPropertyValue("Mock_ReportSuccess").ToLower() == "true";
            string toLog = jci.GetPropertyValue("Mock_StringToLog");
            if (toLog != null)
            {
                jci.LogString(toLog);
            }
            string throwException = jci.GetPropertyValue("Mock_ThrowException");
            if (throwException != null)
            {
                throw new Exception(throwException);
            }
            return er;
        }
    }

    public class MockJobRunnerWithDependency : JobRunner
    {
        public ExecutionResult Execute(JobClientInterface jci)
        {
            jci.LogString("AppDomain: " + AppDomain.CurrentDomain.FriendlyName);
            NUnit.Framework.Assert.IsTrue(true);
            ExecutionResult er = new ExecutionResult();
            er.Success = jci.GetPropertyValue("Mock_ReportSuccess").ToLower() == "true";
            string toLog = jci.GetPropertyValue("Mock_StringToLog");
            if (toLog != null)
            {
                jci.LogString(toLog);
            }
            string throwException = jci.GetPropertyValue("Mock_ThrowException");
            if (throwException != null)
            {
                throw new Exception(throwException);
            }
            return er;
        }
    }
}
