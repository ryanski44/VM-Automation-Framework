using System;
using System.Collections.Generic;
using System.Text;
using JobManagerClient;
using NUnit.Framework;

namespace TestJobManager
{
    public class MockSystem : ISystem
    {
        public MockSystem() { Events = new List<MockEvent>(); }

        public List<MockEvent> Events;

        public void Shutdown(bool restart)
        {
            if (restart)
            {
                Events.Add(new MockEvent(MockEventType.Restart, null));
            }
            else
            {
                Events.Add(new MockEvent(MockEventType.Shutdown, null));
            }
        }
        public void UpdateStatus(string text, bool log)
        {
            if (log)
            {
                Events.Add(new MockEvent(MockEventType.UpdateStatusAndLog, text));
            }
            else
            {
                Events.Add(new MockEvent(MockEventType.UpdateStatus, text));
            }
        }
        public void UpdateStatus(string text)
        {
            UpdateStatus(text, true);
        }

        public void VerifyEventOccured(MockEventType eventType, string eventText, bool partialMatch)
        {
            bool found = false;
            foreach (MockEvent evnt in Events)
            {
                if (partialMatch)
                {
                    if (evnt.EventType == eventType && evnt.Payload.Contains(eventText))
                    {
                        found = true;
                        break;
                    }
                }
                else
                {
                    if (evnt.EventType == eventType && evnt.Payload == eventText)
                    {
                        found = true;
                        break;
                    }
                }
            }
            Assert.True(found, "Could not find event of type \"{0}\" with text \"{1}\"", eventType, eventText);
        }
    }

    public struct MockEvent
    {
        public MockEventType EventType;
        public DateTime Time;
        public string Payload;
        public MockEvent(MockEventType type, string payload)
        {
            Time = DateTime.Now;
            EventType = type;
            Payload = payload;
        }
    }

    public enum MockEventType
    {
        Shutdown,
        Restart,
        UpdateStatus,
        UpdateStatusAndLog
    }
}
