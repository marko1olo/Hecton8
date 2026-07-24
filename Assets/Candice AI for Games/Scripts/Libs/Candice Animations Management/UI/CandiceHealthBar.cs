//System
using System;
using System.Collections;
using System.Collections.Generic;
//Unity
using UnityEngine;
using UnityEngine.UI;
//Candice AI
using CandiceAIforGames.AI;

namespace CandiceAIforGames.AI
{

    public class CandiceHealthBar : MonoBehaviour
    {
		[Header("Colors")]
		[SerializeField] private Color m_MainColor = Color.white;
		[SerializeField] private Color m_FillColor = Color.green;

		[Header("General")]
		[SerializeField] private int m_NumberOfSegments = 5;
		[SerializeField] private float m_SizeOfNotch = 5;
		[Range(0, 1f)] [SerializeField] [UnityEngine.Serialization.FormerlySerializedAs("m_FillAmount")]
		private float _fillAmount = 0.0f;

		public float m_FillAmount
		{
			get { return _fillAmount; }
			set
			{
				if (_fillAmount != value)
				{
					_fillAmount = value;
					UpdateSegments();
				}
			}
		}

		private RectTransform m_RectTransform;
		private Image m_Image;
		private List<Image> m_ProgressToFill = new List<Image>();
		private float m_SizeOfSegment;

		public void Awake()
		{
			// get rect transform
			m_RectTransform = GetComponent<RectTransform>();

			// get image
			m_Image = GetComponentInChildren<Image>();
			m_Image.color = m_MainColor;
			m_Image.gameObject.SetActive(false);

			// count size of segments
			m_SizeOfSegment = m_RectTransform.sizeDelta.x / m_NumberOfSegments;
			for (int i = 0; i < m_NumberOfSegments; i++)
			{
				Image segmentImage = Instantiate(m_Image, transform.position, Quaternion.identity, transform);
				segmentImage.gameObject.SetActive(true);

				segmentImage.fillAmount = m_SizeOfSegment;

				RectTransform segmentRectTransform = segmentImage.rectTransform;
				segmentRectTransform.sizeDelta = new Vector2(m_SizeOfSegment, segmentRectTransform.sizeDelta.y);
				segmentRectTransform.position += (Vector3.right * i * m_SizeOfSegment) - (Vector3.right * m_SizeOfSegment * (m_NumberOfSegments / 2)) + (Vector3.right * i * m_SizeOfNotch);

				Image segmentFillImage = segmentImage.transform.GetChild(0).GetComponent<Image>();
				segmentFillImage.color = m_FillColor;
				m_ProgressToFill.Add(segmentFillImage);

				RectTransform segmentFillRectTransform = segmentFillImage.rectTransform;
				segmentFillRectTransform.sizeDelta = new Vector2(m_SizeOfSegment, segmentFillRectTransform.sizeDelta.y);
			}

			UpdateSegments();
		}

		private void UpdateSegments()
		{
			for (int i = 0; i < m_NumberOfSegments; i++)
			{
				if (i < m_ProgressToFill.Count)
				{
					m_ProgressToFill[i].fillAmount = m_NumberOfSegments * _fillAmount - i;
				}
			}
		}

		private float ConvertFragmentToWidth(float fragment)
		{
			return m_RectTransform.sizeDelta.x * fragment;
		}
	}
}
