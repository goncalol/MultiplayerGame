using UnityEngine;
using UnityEngine.UI;

public class FPSDisplay : MonoBehaviour
{
	float deltaTime = 0.0f;
	public Text t;

	void Update()
	{
		deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
		float fps = 1.0f / deltaTime;
		t.text = fps.ToString();
	}

}
