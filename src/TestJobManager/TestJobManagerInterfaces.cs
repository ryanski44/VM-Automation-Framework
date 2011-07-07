using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using System.Messaging;
using JobManagerInterfaces;

namespace TestJobManager
{
    [TestFixture]
    public class TestJobManagerInterfaces
    {
        //[Test]
        //public void TestSendRecieveJob()
        //{
        //    string path = @".\Private$\" + Guid.NewGuid().ToString();
        //    MessageQueue.Create(path);
        //    try
        //    {

        //        MessageSendRecieve sut = new MessageSendRecieve(path);
        //        Job expected = new Job(OperatingSystemConfiguration.WinXP_32, "alsjdgksofjaopsdfj", "asdfjsjf a sdkljfakl lasdjf al df", "askdfjsk a;sdfjasdf k;l;aksdfj");
        //        string messageID = sut.QueueJob(expected);

        //        //emulate job manager portion
        //        using (MessageQueue jobmanager = MessageSendRecieve.GetSpecificMessageQueue(path))
        //        {
        //            Message incoming = jobmanager.Receive(); 
        //            Job actual = (Job)incoming.Body;
        //            actual.OriginalMessageID = incoming.Id;
        //            Assert.That(actual, Is.Not.Null);
        //            Assert.That(actual.Configuration, Is.EqualTo(expected.Configuration));
        //            Assert.That(actual.ClassName, Is.EqualTo(expected.ClassName));
        //            Assert.That(actual.DLLRelativePath, Is.EqualTo(expected.DLLRelativePath));
        //            Assert.That(actual.ISOUNCPath, Is.EqualTo(expected.ISOUNCPath));
        //            using (MessageQueue sender = MessageSendRecieve.GetSpecificMessageQueue(@".\Private$\" + MessageSendRecieve.LocalQueueName))
        //            {
        //                Message m = new Message(new JobCompletedMessage(actual, new JobResult()));
        //                m.CorrelationId = actual.OriginalMessageID;
        //                sender.Send(m);
        //            }
        //        }

        //        JobCompletedMessage jcm = sut.WaitForJobCompletion(messageID);

        //        Assert.That(jcm, Is.Not.Null);
        //        Assert.That(jcm.Job.ID, Is.EqualTo(expected.ID));
        //    }
        //    finally
        //    {
        //        MessageQueue.Delete(path);
        //    }
        //}

        //[Test]
        //public void TestSendRecieveJobDifferentMSR()
        //{
        //    string path = @".\Private$\" + Guid.NewGuid().ToString();
        //    MessageQueue.Create(path);
        //    try
        //    {

        //        MessageSendRecieve sut1 = new MessageSendRecieve(path);
        //        Job expected = new Job(OperatingSystemConfiguration.WinXP_32, "alsjdgksofjaopsdfj", "asdfjsjf a sdkljfakl lasdjf al df", "askdfjsk a;sdfjasdf k;l;aksdfj");
        //        sut1.QueueJob(expected);

        //        MessageSendRecieve sut2 = new MessageSendRecieve(path);

        //        Job actual = sut2.ReceiveMessage() as Job;
        //        Assert.That(actual, Is.Not.Null);
        //        Assert.That(actual.Configuration, Is.EqualTo(expected.Configuration));
        //        Assert.That(actual.ClassName, Is.EqualTo(expected.ClassName));
        //        Assert.That(actual.DLLUNCPath, Is.EqualTo(expected.DLLUNCPath));
        //        Assert.That(actual.ISOUNCPath, Is.EqualTo(expected.ISOUNCPath));
        //    }
        //    finally
        //    {
        //        MessageQueue.Delete(path);
        //    }
        //}
    }
}
