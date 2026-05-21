// Star solver log level:
// 0 - do not log
// 1 - log errors and warnings only
// 2 - log everything
LogLevel    1

///////////////////////////////////////////////////////////
//               Pulsars with exoplanets                 //
///////////////////////////////////////////////////////////

Star	"PSR B1620-26 A"
{
	ParentBody  "PSR B1620-26"
	Class       "Q"
	AppMagn     24
	MassSol     1.35
	Radius      10.69575

	RotationPeriod 2.7778e-6

	Orbit
	{
		Period          0.4928
		SemiMajorAxis   0.2012  // mass ratio 1.35:0.34
		Eccentricity    0.592
		Inclination     55
		ArgOfPericenter 205
		MeanAnomaly     0
	}
}

Star	"PSR B1620-26 B/WD B1620-26"
{
	ParentBody  "PSR B1620-26"
	Class       "DA2"
	AppMagn     24
	MassSol     0.34

	Orbit
	{
		Period          0.4928
		SemiMajorAxis   0.7988  // mass ratio 1.35:0.34
		Eccentricity    0.592
		Inclination     55
		ArgOfPericenter 25
		MeanAnomaly     0
	}
}

Star	"PSR J1300+1240"	// = PSR B1257+12
{
	ParentBody  "Lich"
	Class       "Neutron"
	MassSol      1.5
	Radius       14
	Age          3

	RotationPeriod  1.727e-6
	Obliquity       50
}

Star	"PSR J1719-1438"
{
	ParentBody  "PSR J1719-14"
	Class       "Neutron"
	MassSol     1.4
	Teff        4500
	Age         12.5

	RotationPeriod	1.61e-6
}

///////////////////////////////////////////////////////////
//           Various pulsars with proper names           //
///////////////////////////////////////////////////////////

Star	"Crab Pulsar"
{
	ParentBody  "PSR B0531+21"
	Class       "Q"
	AppMagn     16.5
	MassSol     1.4
	Radius      10
	Teff        16.e6
	Age         1e-6

	RotationPeriod	9.306349528e-6

	AccretionDisk
	{
		Radius        0.0001
		Temperature   100000
		Brightness    1.0
		Density       5000
	}
}

//Cygnus X-3 could be a black hole too
Star "V1521 Cyg/WR 145a"
{
	ParentBody 	"Cygnus X-3 bar"
	Class 	   	"WN8"  		//class from german wiki
	MassSol     39 			//Unknown generic for WN8
	Radius 	    2250000  	//Unknown generic
	Orbit
	{  
		Period 			0.0005468
		ArgOfPericenter 180
		SemiMajorAxis 	0.00076831
		MeanAnomaly 	0
	}
}

Star "Cygnus X-3/X Cyg X-3/RX J2032.3+4057/4U 2030+40"
{
	ParentBody 	"Cygnus X-3 bar"
	Class      	"Q"
	Orbit
	{  
		Period 			0.0005468
		ArgOfPericenter 0
		SemiMajorAxis 	0.02219563
		MeanAnomaly 	0
	}
	AccretionDisk
	{
		Brightness 		1.0
	}
}

Star "HZ Her"
{
	ParentBody 	"Hercules X-3 bar"  
	MassSol 	2
	AppMag 		13.83
	Radius 		2784000
	Orbit
	{  
		Period 			0.0046575342465
		ArgOfPericenter 0
		SemiMajorAxis 	0.016837811
		MeanAnomaly 	0
	}
}

Star "Hercules X-1/4U 1656+35"
{
	ParentBody   	"Hercules X-3 bar"
	Class 			"Q" 			//unknown class
	RotationPeriod 	0.000344444
	Orbit
	{  
		Period 			0.0046575342465
		ArgOfPericenter 180
		SemiMajorAxis 	0.024944905
		MeanAnomaly 	0
	}
	AccretionDisk
	{
		Brightness 1.0
	}
}

Star "Krzeminski's Star"
{
	ParentBody 	"Centaurus X-3 bar"  
	Class 		"O6.5 II"
	MassSol 	20.5
	AppMag 		13.25
	Radius 		8352000
	Orbit
	{  
		Period 			0.005726027397
		ArgOfPericenter 0
		SemiMajorAxis 	0.004982632
		MeanAnomaly 	0
	}
}

Star   "Centaurus X-3/V779 Cen/1RXS J112115.4-603725/4U 1118-60"
{
	ParentBody  "Centaurus X-3 bar"
	Class 		"Q" 
	MassSol 	1.21
	Orbit
	{  
		Period 			0.005726027397
		ArgOfPericenter 180
		SemiMajorAxis	0.084416489
		MeanAnomaly 	0
	}
	AccretionDisk
	{
		Brightness 1.0
	}
}

Star "HD 24534"
{
	ParentBody 	"X Persei bar"
	Class 	  	"O9 V"
	Radius 	   	4176000
	AppMagn 	6.1
	MassSol 	15
	Orbit
	{
		Period 			0.6849
		SemiMajorAxis 	0.1707
		Eccentricity 	0.11
		ArgOfPericenter 0
		MeanAnomaly 	0
	}
}

Star "X Per"
{
	ParentBody  "X Persei bar"
	Class 		"Q"
	MassSol 	1.4
	Orbit
	{
		Period 			0.6849
		SemiMajorAxis 	1.8293
		Eccentricity 	0.11
		ArgOfPericenter 180
		MeanAnomaly 	0
	}
	AccretionDisk
	{
		Brightness 1.0
	}
}

Star "Vela Pulsar/PSR J0835-4510/PSR B0833-45/2FGL J0835.3-4510/HESS J0835-455"
{
	ParentBody   	"PSR J0835-4510 bar"
	Class 			"Q"
	RotationPeriod 	0.000024813440284
	Age 			0.0000113
	AccretionDisk
	{
		Brightness 1.0
	}
}

Star "Geminga Pulsar/PSR J0633+1746/2CG195+04/2FGL J0633.9+1746" 
{
	ParentBody    "PSR J0633+1746 bar"
	Class 		   "Q"
	RotationPeriod 0.000065860956026
	AccretionDisk
	{
		Brightness 1.0
	}
}
