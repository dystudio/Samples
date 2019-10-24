using System.Security.Authentication;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using RabbitMQ.Client;
using System.Net.Security;
using Microsoft.AspNetCore.Hosting;
using System;

namespace RabbitMQ.Controllers
{
    public class RabbitMQController : Controller
    {
        ConnectionFactory _rabbitConnection;
        IHostingEnvironment _env;

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
