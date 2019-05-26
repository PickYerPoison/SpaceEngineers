List<string> armIDs;

Dictionary<string, List<IMyTerminalBlock>> effectors;
Dictionary<string, List<IMyMotorStator>> joints;
Dictionary<string, List<Vector3D>> targets;

Dictionary<IMyShipController, string> controllers;

Vector3 test;

public Program() {

    // The constructor, called only once every session and
    // always before any other method is called. Use it to
    // initialize your script. 
    //     
    // The constructor is optional and can be removed if not
    // needed.
	
	Initialize();
}

public void Initialize() {
	//double[,] testMatrix1 = {{25, 15, -5}, {15, 18, 0}, {-5, 0, 11}};
	//double[,] testMatrix1 = {{25, 15, -5}, {15, 18, 0}, {-5, 0, 11}, {5, 5, 5}};
	//double[,] testMatrix2 = {{18, 22, 54, 42}, {22, 70, 86, 62}, {54, 86, 174, 134}, {42, 62, 134, 106}};
	//printMatrix(multiplyMatrices(testMatrix2, testMatrix1));
	//printMatrix(choleskyDecomposition(testMatrix2));
	//printMatrix(invertMatrix(testMatrix2));
	
	armIDs = new List<string>();
	
	effectors = new Dictionary<string, List<IMyTerminalBlock>>();
	joints = new Dictionary<string, List<IMyMotorStator>>();
	targets = new Dictionary<string, List<Vector3D>>();
	
	controllers = new Dictionary<IMyShipController, string>();
	
	//Load();
	
	List<IMyTerminalBlock> allBlocks = new List<IMyTerminalBlock>();
	GridTerminalSystem.GetBlocks(allBlocks);
	foreach(IMyTerminalBlock block in allBlocks) {
		if(block is IMyMotorStator) {
			string armID = block.CustomData.ToLower().Trim();
			if(armID == "") {
				continue;
			}
			if(!armIDs.Contains(armID)) {
				createArm(armID);
			}
			joints[armID].Add(block as IMyMotorStator);
		}
		if(block.CustomData.ToLower().Contains("effector")) {
			string armID = block.CustomData.Split('\n')[0].ToLower().Trim();
			if(!armIDs.Contains(armID)) {
				createArm(armID);
			}
			effectors[armID].Add(block);
			if(targets[armID].Count < effectors[armID].Count) {
				targets[armID].Add(getEffectorTip(block));
				targets[armID].Add(block.WorldMatrix.Forward);
			}
		}
		if(block is IMyShipController && block.CustomData.ToLower().Contains("controller")) {
			string armID = block.CustomData.Split('\n')[0].ToLower().Trim();
			controllers[block as IMyShipController] = armID;
		}
	}
}

public void reloadArm(string armID) {
	effectors[armID] = new List<IMyTerminalBlock>();
	joints[armID] = new List<IMyMotorStator>();
	targets[armID] = new List<Vector3D>();
	
	List<IMyTerminalBlock> allBlocks = new List<IMyTerminalBlock>();
	GridTerminalSystem.GetBlocks(allBlocks);
	foreach(IMyTerminalBlock block in allBlocks) {
		string blockArmID = block.CustomData.Split('\n')[0].ToLower().Trim();
		if(blockArmID == armID) {
			if(block is IMyMotorStator) {
				joints[armID].Add(block as IMyMotorStator);
			}
			if(block.CustomData.ToLower().Contains("effector")) {
				effectors[armID].Add(block);
				if(targets[armID].Count < effectors[armID].Count) {
					targets[armID].Add(getEffectorTip(block));
					targets[armID].Add(block.WorldMatrix.Forward);
				}
			}
			if(block is IMyShipController && block.CustomData.ToLower().Contains("controller")) {
				controllers[block as IMyShipController] = armID;
			}
		}
	}
}

public void createArm(string armID) {
	armIDs.Add(armID);
	effectors[armID] = new List<IMyTerminalBlock>();
	joints[armID] = new List<IMyMotorStator>();
	targets[armID] = new List<Vector3D>();
}

public void Save() {

    // Called when the program needs to save its state. Use
    // this method to save your state to the Storage field
    // or some other means. 
    // 
    // This method is optional and can be removed if not
    // needed.
	
	//Storage = "";
	//foreach(Vector3D target in targets) {
	//	if(Storage != "") {
	//		Storage += ";";
	//	}
	//	Storage += target.X + "," + target.Y + "," + target.Z;
	//}
}

public void Load() {
	//if(Storage == "") {
	//	return;
	//}
	//string[] targetStrings = Storage.Split(';');
	//for(int i = 0; i < targetStrings.Length; i++) {
	//	string[] targetComponents = targetStrings[i].Split(',');
	//	targets.Add(new Vector3D(double.Parse(targetComponents[0]), double.Parse(targetComponents[1]), double.Parse(targetComponents[2])));
	//}
}



public void Main(string argument) {
	if(armIDs.Count == 0) {
		Initialize();
	}
	foreach(string armID in armIDs) {
		if(joints[armID].Count == 0 || effectors[armID].Count == 0) {
			reloadArm(armID);
		}
	}
	
	if(argument != "") {
		string[] argumentParts = argument.Split(';');
		if(argumentParts[0].ToLower() == "target") {
			setTargets(argument);
		} else if(argumentParts[0].ToLower() == "control") {
			string armID = argumentParts[1];
			MatrixD orientation = MatrixD.Identity;
			
			foreach(IMyShipController controller in controllers.Keys) {
				if(controllers[controller] == armID) {
					orientation = controller.WorldMatrix.GetOrientation();
				}
			}
			
			string direction = argumentParts[2].ToLower();
			if(direction == "left") {
				targets[armID][0] += orientation.Left;
			} else if(direction == "right") {
				targets[armID][0] += orientation.Right;
			} else if(direction == "up") {
				targets[armID][0] += orientation.Up;
			} else if(direction == "down") {
				targets[armID][0] += orientation.Down;
			} else if(direction == "forward") {
				targets[armID][0] += orientation.Forward;
			} else if(direction == "backward") {
				targets[armID][0] += orientation.Backward;
			}
		} else if(argumentParts[0].ToLower() == "orient") {
			string armID = argumentParts[1];
			MatrixD orientation = MatrixD.Identity;
			
			foreach(IMyShipController controller in controllers.Keys) {
				if(controllers[controller] == armID) {
					orientation = controller.WorldMatrix.GetOrientation();
				}
			}
			
			string direction = argumentParts[2].ToLower();
			if(direction == "left") {
				targets[armID][1] = orientation.Left;
			} else if(direction == "right") {
				targets[armID][1] = orientation.Right;
			} else if(direction == "up") {
				targets[armID][1] = orientation.Up;
			} else if(direction == "down") {
				targets[armID][1] = orientation.Down;
			} else if(direction == "forward") {
				targets[armID][1] = orientation.Forward;
			} else if(direction == "backward") {
				targets[armID][1] = orientation.Backward;
			}
		} else if(argumentParts[0].ToLower() == "reset") {
			Initialize();
		}
	}
	
	foreach(IMyShipController controller in controllers.Keys) {
		Echo("Controller is " + controller.CustomName);
		if(controller != null && controller.IsUnderControl && !controller.ControlThrusters) {
			Echo("Controller is ready");
			Vector3 movement = controller.MoveIndicator;
			Echo("Keyboard inputs - Keyboard: " + controller.MoveIndicator.ToString() + ", Mouse: " + controller.RotationIndicator.ToString() + ", Roll: " + controller.RollIndicator.ToString());
			if(movement.Length() > 0) {
				test = movement;
			}
			Echo("Last keyboard input: " + test.ToString());
			Vector3 orientedMovement = Vector3.Transform(movement, controller.WorldMatrix.GetOrientation());
			targets[controllers[controller]][0] += orientedMovement * 0.1f;
		}
	}
	
	//foreach(IMyTerminalBlock effector in effectors) {
	//	Echo(effector.CustomName);
	//	Echo(effector.Position.ToString());
	//	Echo(effector.Max.ToString());
	//	Echo(effector.Min.ToString());
	//	Echo(effector.Orientation.ToString());
	//}
	
    // The main entry point of the script, invoked every time
    // one of the programmable block's Run actions are invoked.
    // 
    // The method itself is required, but the argument above
    // can be removed if not needed.
	
	foreach(string armID in armIDs) {
		Echo("Processing arm: " + armID);
		double[] updates = getUpdateLeastSquares(armID, 0.1);
		//double[] updates = getUpdateTranspose();
		//printVector(updates);
		for(int i = 0; i < updates.Length; i++) {
			if(!double.IsNaN(updates[i])) {
				joints[armID][i].SetValueFloat("Velocity", Math.Min(Math.Max(-(float)updates[i] * 50, -25), 25));
			}
		}
	}
}

public void setTargets(string argument) {
	string[] argumentParts = argument.Split(';');
	string armID = argumentParts[1];
	targets[armID] = new List<Vector3D>();
	for(int i = 2; i < argumentParts.Length; i++) {
		string[] targetComponents = argumentParts[i].Split(',');
		targets[armID].Add(new Vector3D(double.Parse(targetComponents[0]), double.Parse(targetComponents[1]), double.Parse(targetComponents[2])));
	}
}

public double[] calculateError(string armID) {
	Echo("calculateError");
	double[] errorValues = new double[effectors[armID].Count * 6];
	for(int i = 0; i < effectors[armID].Count * 6; i += 6) {
		IMyTerminalBlock effector = effectors[armID][i / 6];
		Vector3D effectorPosition = getEffectorTip(effector);
		Vector3D targetPosition = targets[armID][i / 3];
		Vector3D positionError = targetPosition - effectorPosition;
		errorValues[i] = positionError.X;
		errorValues[i + 1] = positionError.Y;
		errorValues[i + 2] = positionError.Z;
		
		Vector3D effectorOrientation = Vector3D.Transform(Vector3D.Forward, effector.WorldMatrix.GetOrientation());
		Vector3D targetOrientation = targets[armID][i / 3 + 1];
		Vector3D orientationError = effectorOrientation.Cross(targetOrientation);
		//printVector(effectorOrientation);
		//printVector(targetOrientation);
		//printVector(orientationError);
		errorValues[i + 3] = orientationError.X;
		errorValues[i + 4] = orientationError.Y;
		errorValues[i + 5] = orientationError.Z;
	}
	printVector(errorValues);
	Echo("calculateError end");
	return errorValues;
}

public double[,] calculateJacobian(string armID) {
	Echo("calculateJacobian");
	double[,] jacobian = new double[effectors[armID].Count * 6, joints[armID].Count];
	for(int i = 0; i < effectors[armID].Count * 6; i += 6) {
		IMyTerminalBlock effector = effectors[armID][i / 6];
		Vector3D effectorPosition = getEffectorTip(effector);
		for(int j = 0; j < joints[armID].Count; j++) {
			IMyMotorStator rotor = joints[armID][j];
			
			Matrix rotorOrientation;
			rotor.Orientation.GetMatrix(out rotorOrientation);
			Vector3D rotationAxis = Vector3D.Transform(Vector3D.Up, rotor.WorldMatrix.GetOrientation());
			Vector3D jointPosition = rotor.GetPosition();
			Vector3D partialDerivative = rotationAxis.Cross(effectorPosition - jointPosition);

			jacobian[i, j] = partialDerivative.X;
			jacobian[i + 1, j] = partialDerivative.Y;
			jacobian[i + 2, j] = partialDerivative.Z;
			jacobian[i + 3, j] = rotationAxis.X;
			jacobian[i + 4, j] = rotationAxis.Y;
			jacobian[i + 5, j] = rotationAxis.Z;
		}
	}
	Echo("calculateJacobian end");
	return jacobian;
}

public double[] getUpdateTranspose(string armID) { //Jacobian Transpose
	Echo("getUpdateTranspose");
	double[,] jacobian = calculateJacobian(armID);	
	double[] error = calculateError(armID);
	double[] JTe = multiplyMatrixVector(transposeMatrix(jacobian), error);
	double[] JJTe = multiplyMatrixVector(jacobian, JTe);
	
	double alphaNumerator = dotProduct(error, JJTe);
	double alphaDenominator = dotProduct(JJTe, JJTe);
	double alpha = alphaNumerator / alphaDenominator;
	
	double[] jointUpdates = new double[joints[armID].Count];
	for(int i = 0; i < joints[armID].Count; i++) {
		jointUpdates[i] = alpha * JTe[i];
	}
	Echo("getUpdateTranspose end");
	double[] confirmError = multiplyMatrixVector(jacobian, jointUpdates);
	//printVector(confirmError);
	return jointUpdates;
}

public double[] getUpdateLeastSquares(string armID, double damping) { //Damped Least Squares
	Echo("getUpdateLeastSquares");
	double[,] jacobian = calculateJacobian(armID);
	double[] error = calculateError(armID);
	double[,] JTJ = multiplyMatrices(transposeMatrix(jacobian), jacobian);
	double[,] toInvert = (double[,])JTJ.Clone();
	for(int i = 0; i < toInvert.GetLength(0); i++) {
		toInvert[i, i] += damping * damping;
	}
	double[,] invertedTerm = invertMatrix(toInvert);
	//printMatrix(invertedTerm);
	double[] JTe = multiplyMatrixVector(transposeMatrix(jacobian), error);
	double[] jointUpdates = multiplyMatrixVector(invertedTerm, JTe);
	Echo("getUpdateLeastSquares end");
	Echo("Expected error:");
	double[] confirmError = multiplyMatrixVector(jacobian, jointUpdates);
	//printVector(confirmError);
	//printMatrix(toInvert);
	return jointUpdates;
}

public double[,] transposeMatrix(double[,] matrix) {
	Echo("transposeMatrix");
	double[,] result = new double[matrix.GetLength(1), matrix.GetLength(0)];
	for(int i = 0; i < matrix.GetLength(0); i++) {
		for(int j = 0; j < matrix.GetLength(1); j++) {
			result[j, i] = matrix[i, j];
		}
	}
	Echo("transposeMatrix end");
	return result;
}

public double[,] multiplyMatrices(double[,] m1, double[,] m2) {
	Echo("multiplyMatrices");
	double[,] result = new double[m1.GetLength(0), m2.GetLength(1)];
	if(m1.GetLength(1) != m2.GetLength(0)) {
		Echo("Invalid matrix multiplication!");
	}
	for(int i = 0; i < m1.GetLength(0); i++) {
		for(int j = 0; j < m2.GetLength(1); j++) {
			double total = 0;
			for(int k = 0; k < m1.GetLength(1); k++) {
				total +=  m1[i, k] * m2[k, j];
			}
			result[i, j] = total;
		}
	}
	Echo("multiplyMatrices end");
	return result;
}

public double[] multiplyMatrixVector(double[,] matrix, double[] vector) {
	Echo("multiplyMatrixVector");
	double[] result = new double[matrix.GetLength(0)];
	Echo(matrix.GetLength(0) + " x " + matrix.GetLength(1) + ", " + vector.Length);
	for(int i = 0; i < matrix.GetLength(0); i++) {
		double total = 0;
		for(int j = 0; j < vector.Length; j++) {
			total += matrix[i, j] * vector[j];
		}
		result[i] = total;
	}
	Echo("multiplyMatrixVector end");
	return result;
}

public double dotProduct(double[] v1, double[] v2) {
	Echo("dotProduct");
	double result = 0;
	for(int i = 0; i < v1.Length; i++) {
		result += v1[i] * v2[i];
	}
	Echo("dotProduct end");
	return result;
}

public double[,] invertMatrix(double[,] matrix) {//Returns the inverse of a positive definite matrix
	Echo("invertMatrix");
	//printMatrix(matrix);
	double[,] lowerTriangular = choleskyDecomposition(matrix);
	
	//printMatrix(lowerTriangular);
	double[,] lowerTriangularInverse = invertLowerTriangular(lowerTriangular);
	//printMatrix(lowerTriangularInverse);
	double[,] result = multiplyMatrices(transposeMatrix(lowerTriangularInverse), lowerTriangularInverse);
	Echo("invertMatrix end");
	return result;
}

public double[,] choleskyDecomposition(double[,] matrix) { //Returns a lower triangular matrix L such that LL^T is equal to the input M.
	Echo("choleskyDecomposition");
	int size = matrix.GetLength(0);
	double[,] result = new double[size, size];
	for(int i = 0; i < size; i++) {
		for(int j = 0; j <= i; j++) {
			if(j == i) {
				double sum = 0;
				for(int k = 0; k < j; k++) {
					sum += result[j, k] * result[j, k];
				}
				if(matrix[i, j] - sum < 0) {
					Echo("Square root of negative number!");
				}
				result[i, j] = Math.Sqrt(Math.Max(matrix[i, j] - sum, 0.1));
			} else {
				double sum = 0;
				for(int k = 0; k < j; k++) {
					sum += result[i, k] * result[j, k];
				}
				if(result[j, j] * (matrix[i, j] - sum) == 0) {
					Echo("Square root of negative number!");
				}
				result[i, j] = 1.0 / result[j, j] * (matrix[i, j] - sum);
			}
		}
	}
	Echo("choleskyDecomposition end");
	return result;
}

public double[,] invertLowerTriangular(double[,] matrix) {
	Echo("invertLowerTriangular");
	int size = matrix.GetLength(0);
	double[,] result = new double[size, size];
	for(int j = 0; j < size; j++) {
		double[] identitySlice = new double[size];
		identitySlice[j] = 1;
		double[] resultSlice = forwardSubstitution(matrix, identitySlice);
		for(int i = 0; i < size; i++) {
			result[i, j] = resultSlice[i];
		}
	}
	Echo("invertLowerTriangular");
	return result;
}

public double[] forwardSubstitution(double[,] matrix, double[] vector) { //Finds a vector x such that Mx = v, where M is the input, a lower-triangular matrix.
	double[] result = new double[vector.Length];
	for(int i = 0; i < vector.Length; i++) {
		result[i] = vector[i];
		for(int j = 0; j < i; j++) {
			result[i] -= matrix[i, j] * result[j];
		}
		result[i] /= matrix[i, i];
	}
	return result;
}

public void printVector(double[] vector) {
	string output = "[";
	for(int i = 0; i < vector.Length; i++) {
		output += vector[i].ToString();
		if(i + 1 < vector.Length) {
			output += ",\n";
		}
	}
	Echo(output + "]");
}

public void printVector(Vector3D vector) {
	Echo("[" + vector.X + ",\n" + vector.Y + ",\n" + vector.Z + "]");
}

public void printMatrix(double[,] matrix) {
	string output = "";
	for(int i = 0; i < matrix.GetLength(0); i++) {
		output += "[";
		for(int j = 0; j < matrix.GetLength(1); j++) {
			output += Math.Round(matrix[i, j], 4).ToString();
			if(j + 1 < matrix.GetLength(0)) {
				output += ",\t";
			}
		}
		output += "]";
		if(i + 1 < matrix.GetLength(1)) {
			output += "\n";
		}
	}
	Echo(output);
	Me.CustomData = output;
}

public Vector3D getEffectorTip(IMyTerminalBlock effector) {
	Vector3D forward = Base6Directions.GetVector(effector.Orientation.Forward);
	Vector3D effectorCenter = ((Vector3D)effector.Min + (Vector3D)effector.Max) / 2;
	Vector3I effectorSize = effector.Max - effector.Min;
	int effectorLength = effectorSize.AxisValue(Base6Directions.GetAxis(effector.Orientation.Forward));
	Vector3D worldEffectorCenter = Vector3D.Transform(effectorCenter, effector.WorldMatrix);
	Vector3D worldEffectorForward = Vector3D.Transform(effectorCenter, effector.WorldMatrix.GetOrientation());
	return worldEffectorCenter + worldEffectorForward * (effectorLength / 2);
}