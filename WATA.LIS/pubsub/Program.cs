using System;
using System.Text;
using System.Threading;
using NetMQ;
using NetMQ.Sockets;

class Program
{
    static void Main(string[] args)
    {
        using (var publisher = new PublisherSocket())
        {
            // 퍼블리셔 소켓을 5555 포트에 바인딩합니다.
            publisher.Bind("tcp://127.0.0.1:5002");

            Console.WriteLine("Publisher started...");

            while (true)
            {
                // 메시지를 퍼블리시합니다.
                string message = "LIS>MID360,1";

                // 주제와 메시지를 결합하여 퍼블리시
                publisher.SendFrame(message);
                Console.WriteLine($"Published: {message}");



                // 1초 대기
                Thread.Sleep(1000);
            }
        }
    }
}
/*
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
            subscriber.Connect("tcp://127.0.0.1:5555");

            // "VISION" 주제를 구독합니다.
            subscriber.Subscribe("MID360>LIS");

            Console.WriteLine("Subscriber started. Waiting for messages...");

            while (true)
            {
                // 메시지를 수신합니다.
                string topic = subscriber.ReceiveFrameString();
                string message = subscriber.ReceiveFrameString();

                Console.WriteLine($"Received: {topic} {message}");
            }
        }
    }
}
*/