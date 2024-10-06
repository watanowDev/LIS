using System;
using NetMQ;
using NetMQ.Sockets;

class Program
{
    static void Main(string[] args)
    {
        using (var subscriber = new SubscriberSocket())
        {
            // 서브스크라이버 소켓을 5555 포트에 연결합니다.
            subscriber.Connect("tcp://192.168.219.186:5001");

            // "VISION" 주제를 구독합니다.
            subscriber.Subscribe("MID360>LIS");

            Console.WriteLine("Subscriber started. Waiting for messages...");

            while (true)
            {
                // 메시지를 수신합니다.
                string topic = subscriber.ReceiveFrameString();
                string message = subscriber.ReceiveFrameString();

                Console.WriteLine($"Received: {topic} {message} {DateTime.Now.ToString("MM-dd HH:mm:ss")}");
            }
        }
    }
}