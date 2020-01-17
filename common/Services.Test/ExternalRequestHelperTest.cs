// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Mmm.Platform.IoT.Common.Services.Helpers;
using Mmm.Platform.IoT.Common.Services.Http;
using Mmm.Platform.IoT.Common.Services.Test.Models;
using Mmm.Platform.IoT.Common.TestHelpers;
using Moq;
using Newtonsoft.Json;
using Xunit;
using HttpResponse = Mmm.Platform.IoT.Common.Services.Http.HttpResponse;

namespace Mmm.Platform.IoT.Common.Services.Test
{
    public class ExternalRequestHelperTest
    {
        private const string MOCK_SERVICE_URI = @"http://mockuri";
        private const string AZDS_ROUTE_KEY = "azds-route-as";

        private readonly Mock<IHttpClient> mockHttpClient;
        private readonly Mock<IHttpContextAccessor> mockHttpContextAccessor;
        private readonly IExternalRequestHelper externalRequestHelper;
        private readonly Random rand;

        public ExternalRequestHelperTest()
        {
            this.mockHttpClient = new Mock<IHttpClient>();
            this.mockHttpContextAccessor = new Mock<IHttpContextAccessor>();

            this.mockHttpContextAccessor
                .Setup(t => t.HttpContext.Request.HttpContext.Items)
                .Returns(new Dictionary<object, object>() { { "TenantID", "test_tenant" } });
            this.mockHttpContextAccessor
                .Setup(t => t.HttpContext.Request.Headers)
                .Returns(new HeaderDictionary() { { AZDS_ROUTE_KEY, "mockDevSpace" } });
            this.rand = new Random();

            this.externalRequestHelper = new ExternalRequestHelper(
                this.mockHttpClient.Object,
                this.mockHttpContextAccessor.Object);
        }

        [Fact, Trait(Constants.TYPE, Constants.UNIT_TEST)]
        public async Task ProcessNoModelRequest()
        {
            string path = this.rand.NextString();
            string url = $"{MOCK_SERVICE_URI}/{path}";

            HttpMethod method = HttpMethod.Get;

            HttpResponse response = new HttpResponse
            {
                StatusCode = HttpStatusCode.OK,
                IsSuccessStatusCode = true,
            };

            this.mockHttpClient
                .Setup(x => x.SendAsync(It.IsAny<IHttpRequest>(), It.IsAny<HttpMethod>()))
                .ReturnsAsync(response);
            
            await this.externalRequestHelper.ProcessRequestAsync(method, url);

            this.mockHttpClient
                .Verify(x => x.SendAsync(
                    It.Is<IHttpRequest>(r => r.Check(url)),
                    It.Is<HttpMethod>(r => r == method)),
                Times.Once);
        }


        [Fact, Trait(Constants.TYPE, Constants.UNIT_TEST)]
        public async Task ProcessModelRequest()
        {
            string path = this.rand.NextString();
            string url = $"{MOCK_SERVICE_URI}/{path}";

            string value = this.rand.NextString();
            ExternalRequestModel content = new ExternalRequestModel
            {
                value = value
            };

            HttpMethod method = HttpMethod.Get;

            HttpResponse response = new HttpResponse
            {
                StatusCode = HttpStatusCode.OK,
                IsSuccessStatusCode = true,
                Content = JsonConvert.SerializeObject(content)
            };

            this.mockHttpClient
                .Setup(x => x.SendAsync(
                    It.IsAny<IHttpRequest>(),
                    It.IsAny<HttpMethod>()))
                .ReturnsAsync(response);
            
            ExternalRequestModel processedResponse = await this.externalRequestHelper.ProcessRequestAsync(method, url, content);

            this.mockHttpClient
                .Verify(x => x.SendAsync(
                    It.Is<IHttpRequest>(r => r.Check(url)),
                    It.Is<HttpMethod>(r => r == method)),
                Times.Once);

            Assert.Equal(processedResponse.value, value);
        }
    }
}