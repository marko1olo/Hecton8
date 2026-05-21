// Star solver log level:
// 0 - do not log
// 1 - log errors and warnings only
// 2 - log everything
LogLevel    1

///////////////////////////////////////////////////////////
//               Pulsars with exoplanets                 //
///////////////////////////////////////////////////////////

StarBarycenter	"PSR B1620-26"
{
	RA      16.39388889
	Dec     -26.5313889
	Dist    2200
	AppMagn 24
	Class  "Neutron"
}

StarBarycenter	"Lich/PSR B1257+12"	// = PSR J1300+1240
{
	RA      13.00099353
	Dec     12.682353
	Dist    600
	Class  "Neutron"
}

StarBarycenter	"PSR J1719-14"
{
	RA      17.31946413
	Dec     -14.6336
	Dist    1200
	Class  "Q"
	MassSol 1.4
	Teff    4500
	Age     12.5
}

///////////////////////////////////////////////////////////
//           Various pulsars with proper names           //
///////////////////////////////////////////////////////////

// Crab nebula pulsar
StarBarycenter  "PSR B0531+21"
{
	RA      05 34 31.97
	Dec     22 00 52.1
	Dist    2000
}

Star	"Black Widow/PSR B1957+20"
{
	RA      19.99361111
	Dec     20.80416669
	Dist    1530
	Class  "Q"
	MassSol 1.4
	Age     2.2
}

Star	"PSR J1807-2459 A"
{
	RA      10.12222222
	Dec     -24.99805559
	Dist    2790
	Class  "Q"
	MassSol 1.4
}

Star	"PSR J2051-0827"
{
	RA      20.85222222
	Dec     -8.460555573
	Dist    1280
	Class  "Q"
	MassSol 1.4
	Age     5.61
}

Star	"PSR J2241-5236"
{
	RA      22.695
	Dec     -52.61000002
	Dist    500
	Class  "Q"
	MassSol 1.35
	Age     5.22
}

StarBarycenter  "Cygnus X-3 bar"
{
	RA   20 32 25.78
	Dec  +40 57 27.9
	Dist 11300
}

StarBarycenter  "Hercules X-3 bar"
{
	RA   16 57 49.83
	Dec  +35 20 32.6
	Dist 4600  
}

StarBarycenter  "Centaurus X-3 bar"
{
	RA   11 21 15.78
	Dec  -60 37 22.7
	Dist 5700
}

RemoveStar "HD 24534"

StarBarycenter "X Persei bar/HR 1209"
{
	RA   3 55 23.08
	Dec  +31 2 45
	Dist 828.2209
}

//Vela
StarBarycenter  "PSR J0835-4510 bar"
{
	RA   08 35 20.61149
	Dec  -45 10 34.8751
	Dist 280
}

//Geminga
StarBarycenter  "PSR J0633+1746 bar"
{
	RA   06 33 54.1530
	Dec  +17 46 12.909
	Dist 250
}
