using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ModelingEvolution.Drawing;

namespace ModelingEvolution.BlazorBlaze.VectorGraphics
{
    public static class ContainerExtensions
    {
       
        public static List<U> ToList<T,U>(this List<T> list, Func<T, U> transformation)
        {
            var transformedList = new List<U>(list.Count);
            for (var index = 0; index < list.Count; index++)
            {
                var item = list[index];
                transformedList.Add(transformation(item));
            }

            return transformedList;
        }
        public static IServiceCollection AddVectorGraphicsStreaming(this IServiceCollection services)
        {
            services.AddTransient<StreamingCanvasEngine>();
            services.AddSingleton<RemoteCanvasStreamPool>();
            return services;
        }
    }
}
