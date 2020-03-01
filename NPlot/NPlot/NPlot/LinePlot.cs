/*
 * NPlot - A charting library for .NET
 * 
 * LinePlot.cs
 * Copyright (C) 2003-2006 Matt Howlett and others.
 * All rights reserved.
 * 
 * Redistribution and use in source and binary forms, with or without modification,
 * are permitted provided that the following conditions are met:
 * 
 * 1. Redistributions of source code must retain the above copyright notice, this
 *	  list of conditions and the following disclaimer.
 * 2. Redistributions in binary form must reproduce the above copyright notice,
 *	  this list of conditions and the following disclaimer in the documentation
 *	  and/or other materials provided with the distribution.
 * 3. Neither the name of NPlot nor the names of its contributors may
 *	  be used to endorse or promote products derived from this software without
 *	  specific prior written permission.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
 * ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.
 * IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT,
 * INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING,
 * BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
 * DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
 * LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE
 * OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED
 * OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Drawing;
using System.Diagnostics;

namespace NPlot
{

	/// <summary>
	/// Encapsulates functionality for plotting data as a line chart.
	/// </summary>
	public class LinePlot : BaseSequencePlot, IPlot, ISequencePlot
	{

		/// <summary>
		/// Default constructor
		/// </summary>
		public LinePlot()
		{
		}


		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="dataSource">The data source to associate with this plot</param>
		public LinePlot( object dataSource )
		{
			this.DataSource = dataSource;
		}


		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="ordinateData">the ordinate data to associate with this plot.</param>
		/// <param name="abscissaData">the abscissa data to associate with this plot.</param>
		public LinePlot( object ordinateData, object abscissaData )
		{
			this.OrdinateData = ordinateData;
			this.AbscissaData = abscissaData;
		}


		/// <summary>
		/// Draws the line plot on a GDI+ surface against the provided x and y axes.
		/// </summary>
		/// <param name="g">The GDI+ surface on which to draw.</param>
		/// <param name="xAxis">The X-Axis to draw against.</param>
		/// <param name="yAxis">The Y-Axis to draw against.</param>
		/// <param name="drawShadow">If true draw the shadow for the line. If false, draw line.</param>
		public void DrawLineOrShadow( Graphics g, PhysicalAxis xAxis, PhysicalAxis yAxis, bool drawShadow )
		{
			Pen shadowPen = null;
			if (drawShadow)
			{
				shadowPen = (Pen)this.Pen.Clone();
				shadowPen.Color = this.ShadowColor;
			}

			SequenceAdapter data = 
				new SequenceAdapter( this.DataSource, this.DataMember, this.OrdinateData, this.AbscissaData );

			ITransform2D t = Transform2D.GetTransformer( xAxis, yAxis );
			
			int numberPoints = data.Count;
			
			if (data.Count == 0)
			{
				return;
			}

			// clipping is now handled assigning a clip region in the
			// graphic object before this call
			if (numberPoints == 1)
			{
				PointF physical = t.Transform( data[0] );
				
				if (drawShadow)
				{
					g.DrawLine( shadowPen, 
						physical.X - 0.5f + this.ShadowOffset.X,
						physical.Y + this.ShadowOffset.Y,
						physical.X + 0.5f + this.ShadowOffset.X,
						physical.Y + this.ShadowOffset.Y );
				}
				else
				{
					g.DrawLine( Pen, physical.X-0.5f, physical.Y, physical.X+0.5f, physical.Y);
				}
			}
			else
			{
				// prepare for clipping
				double leftCutoff = xAxis.PhysicalToWorld(xAxis.PhysicalMin, false);
				double rightCutoff = xAxis.PhysicalToWorld(xAxis.PhysicalMax, false);
				if (leftCutoff > rightCutoff)
				{
					Utils.Swap(ref leftCutoff, ref rightCutoff);
				}
				if (drawShadow)
				{
					// correct cut-offs
					double shadowCorrection =
						xAxis.PhysicalToWorld(ShadowOffset, false) - xAxis.PhysicalToWorld(new Point(0,0), false);
					leftCutoff -= shadowCorrection;
					rightCutoff -= shadowCorrection;
				}

                int start = Utils.GetDataIndex(data, leftCutoff);
                int end = Math.Min(Utils.GetDataIndex(data, rightCutoff) + 1, (data.Count - 1)); // Need to add one here so that the plot will extend off the right side of the screen
                PointF p1;
                for(; start < end; start++)
                {
                    // Ensure the starting point is valid
                    var startPoint = data[start];
                    if(Double.IsNaN(startPoint.X) || Double.IsNaN(startPoint.Y))
                    {
                        continue;
                    }

                    p1 = t.Transform(startPoint);
                    if(!float.IsNaN(p1.X) && !float.IsNaN(p1.Y))
                    {
                        break;
                    }
                }
                PointF p2 = p1;
                const float spacing = 1.0f;
                float avgCount = 1.0f;
                float min = p1.Y;
                float max = p1.Y;
                for (int i = start + 1; i <= end; ++i)
				{
                    // check to see if any values null. If so, then continue.
                    var point = data[i];
					if(Double.IsNaN(point.X) || Double.IsNaN(point.Y))
					{
						continue;
					}
                    PointF p = t.Transform(point);
                    if(float.IsNaN(p.X) || float.IsNaN(p.Y))
                    {
                        continue;
                    }

                    // Wait until there is at least a pixel between the previous line and the next one.
                    // Otherwise it takes longer to draw the sub-points, and g.DrawLine can throw an overflow exception
                    p2 = new PointF(p.X, p2.Y - (p2.Y / avgCount) + (p.Y / avgCount));
                    min = Math.Min(p.Y, min);
                    max = Math.Max(p.Y, max);
                    avgCount++;
                    if(p.X < (p1.X + spacing))
                    {
                        continue;
                    }
                    p2.Y = (p2.Y >= p1.Y) ? max : min;

					if (drawShadow)
					{
						g.DrawLine( shadowPen, 
							p1.X + ShadowOffset.X,
							p1.Y + ShadowOffset.Y,
							p2.X + ShadowOffset.X,
							p2.Y + ShadowOffset.Y );
					}
					else
					{
						g.DrawLine( Pen, p1.X, p1.Y, p2.X, p2.Y );
					}
                    p1 = p2;
                    min = p1.Y;
                    max = p1.Y;
                    avgCount = 1.35f;   // Average in a little of the last point to smooth roll-overs between pixel boundaries
                }
			}
		}


		/// <summary>
		/// Draws the line plot on a GDI+ surface against the provided x and y axes.
		/// </summary>
		/// <param name="g">The GDI+ surface on which to draw.</param>
		/// <param name="xAxis">The X-Axis to draw against.</param>
		/// <param name="yAxis">The Y-Axis to draw against.</param>
		public void Draw( Graphics g, PhysicalAxis xAxis, PhysicalAxis yAxis )
		{
			if (this.shadow_)
			{
				this.DrawLineOrShadow( g, xAxis, yAxis, true );
			}

			this.DrawLineOrShadow( g, xAxis, yAxis, false );
		}


		/// <summary>
		/// Returns an x-axis that is suitable for drawing this plot.
		/// </summary>
		/// <returns>A suitable x-axis.</returns>
		public Axis SuggestXAxis()
		{
			SequenceAdapter data_ = 
				new SequenceAdapter( this.DataSource, this.DataMember, this.OrdinateData, this.AbscissaData );

			return data_.SuggestXAxis();
		}


		/// <summary>
		/// Returns a y-axis that is suitable for drawing this plot.
		/// </summary>
		/// <returns>A suitable y-axis.</returns>
		public Axis SuggestYAxis()
		{
			SequenceAdapter data_ = 
				new SequenceAdapter( this.DataSource, this.DataMember, this.OrdinateData, this.AbscissaData );

			return data_.SuggestYAxis();
		}


		/// <summary>
		/// If true, draw a shadow under the line.
		/// </summary>
		public bool Shadow
		{
			get
			{
				return shadow_;
			}
			set
			{
				shadow_ = value;
			}
		}
		private bool shadow_ = false;
	

		/// <summary>
		/// Color of line shadow if drawn. Use Shadow method to turn shadow on and off.
		/// </summary>
		public Color ShadowColor
		{
			get
			{
				return shadowColor_;
			}
			set
			{
				shadowColor_ = value;
			}
		}
		private Color shadowColor_ = Color.FromArgb(100,100,100);


		/// <summary>
		/// Offset of shadow line from primary line.
		/// </summary>
		public Point ShadowOffset
		{
			get
			{
				return shadowOffset_;
			}
			set
			{
				shadowOffset_ = value;
			}
		}
		private Point shadowOffset_ = new Point( 1, 1 );


		/// <summary>
		/// Draws a representation of this plot in the legend.
		/// </summary>
		/// <param name="g">The graphics surface on which to draw.</param>
		/// <param name="startEnd">A rectangle specifying the bounds of the area in the legend set aside for drawing.</param>
		public virtual void DrawInLegend(Graphics g, Rectangle startEnd)
		{
			g.DrawLine(pen_, startEnd.Left, (startEnd.Top + startEnd.Bottom) / 2,
				startEnd.Right, (startEnd.Top + startEnd.Bottom) / 2);
		}


		/// <summary>
		/// The pen used to draw the plot
		/// </summary>
		public System.Drawing.Pen Pen
		{
			get
			{
				return pen_;
			}
			set
			{
				pen_ = value;
			}
		}
		private System.Drawing.Pen pen_ = new Pen(Color.Black);


		/// <summary>
		/// The color of the pen used to draw lines in this plot.
		/// </summary>
		public System.Drawing.Color Color
		{
			set
			{
				if (pen_ != null)
				{
					pen_.Color = value;
				}
				else
				{
					pen_ = new Pen(value);
				}
			}
			get
			{
				return pen_.Color;
			}
		}
	}
}
