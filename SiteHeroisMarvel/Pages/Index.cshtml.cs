using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;

namespace SiteHeroisMarvel.Pages
{
    public class IndexModel : PageModel
    {
        private static object syncObject = Guid.NewGuid();
        private static string[] HEROIS = new string[]
        {
            "Captain America", "Iron Man", "Thor", "Hulk",
            "Wolverine", "Spider-Man", "Black Panther",
            "Doctor Strange", "Daredevil"
        };

        public void OnGet(
            [FromServices]IConfiguration config,
            [FromServices]IDistributedCache cache)
        {
            Personagem personagem = null;
            string heroi = HEROIS[new Random().Next(0, 9)];

            string valorJSON = cache.GetString(heroi);
            if (valorJSON == null)
            {
                // Exemplo de implementação do pattern Double-checked locking
                // Para mais informações acesse:
                // https://en.wikipedia.org/wiki/Double-checked_locking
                lock (syncObject)
                {
                    valorJSON = cache.GetString(heroi);
                    if (valorJSON == null)
                    {
                        personagem = ObterPersonagem(config, heroi);

                        DistributedCacheEntryOptions opcoesCache =
                            new DistributedCacheEntryOptions();
                        opcoesCache.SetAbsoluteExpiration(
                            TimeSpan.FromMinutes(20));

                        valorJSON = JsonConvert.SerializeObject(personagem);
                        cache.SetString(heroi, valorJSON, opcoesCache);
                    }
                }
            }
            
            if (personagem == null && valorJSON != null)
            {
                personagem = JsonConvert
                    .DeserializeObject<Personagem>(valorJSON);
            }

            TempData["Personagem"] = personagem;
        }

        private Personagem ObterPersonagem(
            IConfiguration config, string heroi)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));

                string ts = DateTime.Now.Ticks.ToString();
                string publicKey = config.GetSection("MarvelComicsAPI:PublicKey").Value;
                string hash = GerarHash(ts, publicKey,
                    config.GetSection("MarvelComicsAPI:PrivateKey").Value);
                
                string url = config.GetSection("MarvelComicsAPI:BaseURL").Value +
                    $"characters?ts={ts}&apikey={publicKey}&hash={hash}&" +
                    $"name={Uri.EscapeUriString(heroi)}";
                HttpResponseMessage response = client.GetAsync(
                    url).Result;

                response.EnsureSuccessStatusCode();
                string conteudo =
                    response.Content.ReadAsStringAsync().Result;

                dynamic resultado = JsonConvert.DeserializeObject(conteudo);

                Personagem personagem = new Personagem();
                personagem.Nome = resultado.data.results[0].name;
                personagem.Descricao = resultado.data.results[0].description;
                personagem.UrlImagem = resultado.data.results[0].thumbnail.path + "." +
                    resultado.data.results[0].thumbnail.extension;
                personagem.UrlWiki = resultado.data.results[0].urls[1].url;

                return personagem;
            }
        }

        private string GerarHash(
            string ts, string publicKey, string privateKey)
        {
            byte[] bytes =
                Encoding.UTF8.GetBytes(ts + privateKey + publicKey);
            var gerador = MD5.Create();
            byte[] bytesHash = gerador.ComputeHash(bytes);
            return BitConverter.ToString(bytesHash)
                .ToLower().Replace("-", String.Empty);
        }
    }
}