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
            publisher.Bind("tcp://192.168.219.193:5002");

            Console.WriteLine("Publisher started...");
            int messageCount = 0;

            while (true)
            {
                // 메시지를 퍼블리시합니다.
                string topic = "LIS>MID360";
                string message = $"Hello, message {messageCount++}";

                // 주제와 메시지를 결합하여 퍼블리시
                publisher.SendMoreFrame(topic).SendFrame(message);
                Console.WriteLine($"Published: {topic} {message}");

                // 1초 대기
                Thread.Sleep(1000);
            }
        }
    }
}