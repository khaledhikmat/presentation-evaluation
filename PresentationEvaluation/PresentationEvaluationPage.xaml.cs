using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Plugin.Media;
using Plugin.Media.Abstractions;
using Xamarin.Forms;

namespace PresentationEvaluation
{
	public partial class PresentationEvaluationPage : ContentPage
	{
		private const string FacialApi = "https://api.projectoxford.ai/face/v1.0/detect?returnFaceAttributes=age,gender,smile";
		private const string PowerBIApi = "get-your-own";
		private const bool IsCognitive = true;
		private const bool IsPowerBI = true;

		static List<string> genders = new List<string>()
		{
			{"male" },
			{"female" },
			{"female" },
			{"male" },
			{"female" },
			{"male" },
			{"male" },
			{"female" },
			{"male" },
			{"female" },
			{"male" }
		};

		static List<float> ages = new List<float>()
		{
			{(float)18.50},
			{(float)19.10},
			{(float)23.20},
			{(float)25.40},
			{(float)29.40},
			{(float)32.90},
			{(float)39.80},
			{(float)44.56},
			{(float)47.90},
			{(float)49.90},
			{(float)52.54},
			{(float)53.13},
			{(float)54.56},
			{(float)55.90},
			{(float)57.19},
			{(float)59.40},
			{(float)62.34},
			{(float)64.67},
			{(float)65.23}
		};

		static List<float> smiles = new List<float>()
		{
			{(float)0.00},
			{(float)0.10},
			{(float)0.13},
			{(float)0.19},
			{(float)0.22},
			{(float)0.25},
			{(float)0.28},
			{(float)0.31},
			{(float)0.34},
			{(float)0.40},
			{(float)0.45},
			{(float)0.48},
			{(float)0.49},
			{(float)0.53},
			{(float)0.54},
			{(float)0.55},
			{(float)0.59},
			{(float)0.61},
			{(float)0.64},
			{(float)0.65},
			{(float)0.69},
			{(float)0.71},
			{(float)0.73},
			{(float)0.74},
			{(float)0.75},
			{(float)0.78},
			{(float)0.79},
			{(float)0.83},
			{(float)0.85},
			{(float)0.87},
			{(float)0.88},
			{(float)0.89},
			{(float)0.91},
			{(float)0.92},
			{(float)0.94},
			{(float)0.96},
			{(float)0.97},
			{(float)0.99},
			{(float)1.00},
			{(float)1.00}
		};

		private Random random = new Random();

		public PresentationEvaluationPage()
		{
			InitializeComponent();
			ResultPanel.IsVisible = false;
		}

		private async void btnTakePicture_Clicked(object sender, EventArgs e)
		{
			try
			{
				await CrossMedia.Current.Initialize();

				if (!CrossMedia.Current.IsCameraAvailable || !CrossMedia.Current.IsTakeVideoSupported)
					throw new Exception($"There is no camera on the device!");

				var file = await CrossMedia.Current.TakePhotoAsync(new Plugin.Media.Abstractions.StoreCameraMediaOptions
				{
					SaveToAlbum = true,
					Name = "SelfieEvaluation.jpg"
				});

				if (file == null)
					throw new Exception($"Picture not captured to disk!!");

				Indicator.IsVisible = true;
				Indicator.IsRunning = true;
				ResultPanel.IsVisible = false;
				Image.Source = ImageSource.FromStream(() => file.GetStream());
				byte[] picture = GetBytes(file);

				float age = -1;
				string gender = "";
				float smile = -1;

				// Submit to Cognitive
				if (IsCognitive)
				{
					using (var httpClient = new HttpClient())
					{
						httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", "get-your-own");
						HttpResponseMessage response;
						var content = new ByteArrayContent(picture);
						content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
						response = await httpClient.PostAsync(FacialApi, content);
						string responseData = await response.Content.ReadAsStringAsync();
						if (!response.IsSuccessStatusCode)
							throw new Exception($"Unable to post to cognitive service: {response.StatusCode.ToString()}");

						Face[] faces = JsonConvert.DeserializeObject<Face[]>(responseData);
						if (faces != null && faces.Length > 0)
						{
							Face face = faces[0];
							age = face.faceAttributes.age;
							gender = face.faceAttributes.gender;
							smile = face.faceAttributes.smile;
						}
					}
				}
				else
				{
					gender = genders.ElementAt(random.Next(genders.Count - 1));
					age = ages.ElementAt(random.Next(ages.Count - 1));
					smile = smiles.ElementAt(random.Next(smiles.Count - 1));
				}

				AgeData.Text = age + "";
				GenderData.Text = gender;
				SmileData.Text = smile + "";

				// Submit to PowerBI
				if (IsPowerBI && age != -1 && !string.IsNullOrEmpty(gender) && smile != -1)
				{
					using (var httpClient = new HttpClient())
					{
						var realTimeEvent = new
						{
							time = DateTime.Now,
							age = (int)age,
							score = (int)(smile * 10),
							gender = gender
						};

						var data = new dynamic[1];
						data[0] = realTimeEvent;
						var postData = JsonConvert.SerializeObject(data);
						HttpContent httpContent = new StringContent(postData, Encoding.UTF8, "application/json");
						HttpResponseMessage response = await httpClient.PostAsync(PowerBIApi, httpContent);
						string responseString = await response.Content.ReadAsStringAsync();

						if (!response.IsSuccessStatusCode)
						{
							throw new Exception("Unable to post to PowerBI: " + response.StatusCode);
						}
					}
				}
				else
					throw new Exception($"Humm.....no face information! Please try again.");


				Result.Text = $"Thank you! Your evaluation was submitted.";
				ResultPanel.IsVisible = true;
			}
			catch (Exception ex)
			{
				await DisplayAlert("Sorry", "An error occurred: " + ex.Message, "Ok");
			}
			finally
			{
				Indicator.IsVisible = false;
				Indicator.IsRunning = false;
			}
		}

		public static byte[] GetBytes(MediaFile file)
		{
			byte[] fileBytes = null;
			using (var ms = new MemoryStream())
			{
				file.GetStream().CopyTo(ms);
				file.Dispose();
				fileBytes = ms.ToArray();
			}

			return fileBytes;
		}
	}
}



public class Face
{
	public string faceId { get; set; }
	public Facerectangle faceRectangle { get; set; }
	public Faceattributes faceAttributes { get; set; }
	public string glasses { get; set; }
	public Headpose headPose { get; set; }
}

public class Facerectangle
{
	public int width { get; set; }
	public int height { get; set; }
	public int left { get; set; }
	public int top { get; set; }
}

public class Faceattributes
{
	public float age { get; set; }
	public string gender { get; set; }
	public float smile { get; set; }
	public Facialhair facialHair { get; set; }
}

public class Facialhair
{
	public float mustache { get; set; }
	public float beard { get; set; }
	public float sideburns { get; set; }
}

public class Headpose
{
	public float roll { get; set; }
	public int yaw { get; set; }
	public int pitch { get; set; }
}

