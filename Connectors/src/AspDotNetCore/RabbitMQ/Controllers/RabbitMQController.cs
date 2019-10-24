using System.Security.Authentication;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using RabbitMQ.Client;
using System.Net.Security;
using Microsoft.AspNetCore.Hosting;
using System;
using EasyNetQ;
using System.Threading.Tasks;

namespace RabbitMQ.Controllers
{
    public class RabbitMQController : Controller
    {
        ConnectionFactory _rabbitConnection;
        IHostingEnvironment _env;
        IBus _bus;

        public RabbitMQController([FromServices] ConnectionFactory rabbitConnection, IHostingEnvironment env)
        {
            _rabbitConnection = rabbitConnection;
            _env = env;
            SslOption opt = _rabbitConnection.Ssl;
            if (opt != null && opt.Enabled)
            {
                opt.Version = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12;

                // Only needed if want to disable certificate validations
                opt.AcceptablePolicyErrors = SslPolicyErrors.RemoteCertificateChainErrors |
                    SslPolicyErrors.RemoteCertificateNameMismatch | SslPolicyErrors.RemoteCertificateNotAvailable;
            }

            try
            {
                var connectionString = @"host={0};port={1};virtualHost={2};username={3};password={4};requestedHeartbeat={5}";
                _bus = RabbitHutch.CreateBus(string.Format(connectionString,
                    _rabbitConnection.HostName,
                    _rabbitConnection.Port,
                    _rabbitConnection.VirtualHost,
                    _rabbitConnection.UserName,
                    _rabbitConnection.Password,
                    _rabbitConnection.RequestedHeartbeat));
            }
            catch (Exception ex)
            {

                Console.WriteLine("RabbitMQController-CreateBus:" + ex.Message);
            }
        }

    
        public IActionResult Receive()
        {
            //if (_env.IsDevelopment())
            //{
            //    //_rabbitConnection.HostName = "localhost";
            //    _rabbitConnection.HostName = "http://172.16.103.237";
            //}

            try
            {
                using (var connection = _rabbitConnection.CreateConnection())
                using (var channel = connection.CreateModel())
                {
                    CreateQueue(channel);
                    var data = channel.BasicGet("rabbit-test", true);
                    if (data != null)
                    {
                        ViewData["message"] = Encoding.UTF8.GetString(data.Body);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("RabbitMQController-Receive:" + ex.Message);
            }

            _bus.SubscribeAsync<TextMessage>("test", message => Task.Factory.StartNew(() =>
            {
                // Perform some actions here
                // If there is a exception it will result in a task complete but task faulted which
                // is dealt with below in the continuation
            }).ContinueWith(task =>
            {
                if (task.IsCompleted && !task.IsFaulted)
                {
                    // Everything worked out ok
                    ViewData["EasyMessage"] = message;
                }
                else
                {
                    // Don't catch this, it is caught further up the hierarchy and results in being sent to the default error queue
                    // on the broker
                    throw new EasyNetQException("Message processing exception - look in the default error queue (broker)");
                }
            }));


            return View();
        }

        public IActionResult Send(string message)
        {
            //if (_env.IsDevelopment())
            //{
            //    //_rabbitConnection.HostName = "localhost";
            //    _rabbitConnection.HostName = "http://172.16.103.237";
            //}

            if (message != null && message != "") {
                try
                {
                    using (var connection = _rabbitConnection.CreateConnection())
                    using (var channel = connection.CreateModel())
                    {
                        CreateQueue(channel);
                        var body = Encoding.UTF8.GetBytes(message);
                        channel.BasicPublish(exchange: "",
                                             routingKey: "rabbit-test",
                                             basicProperties: null,
                                             body: body);
                    }
                }
                catch (System.Exception ex)
                {
                    Console.WriteLine("RabbitMQController-Send:" + ex.Message);
                }


                try
                {
                    _bus.PublishAsync(new TextMessage
                    {
                        Text = message
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine("RabbitMQController-_bus.PublishAsync:" + ex.Message);
                }
            }
            return View();
        }

        protected void CreateQueue(IModel channel)
        {
            try
            {
                channel.QueueDeclare(queue: "rabbit-test",
                                     durable: false,
                                     exclusive: false,
                                     autoDelete: false,
                                     arguments: null);
            }
            catch (System.Exception ex)
            {
                Console.WriteLine("RabbitMQController-CreateQueue:" + ex.Message);
            }
        }
    }
}
